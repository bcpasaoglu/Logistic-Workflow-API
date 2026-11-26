using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LogisticDesk.DTOs.Auth;
using LogisticDesk.Enums;
using LogisticDesk.Models;
using Xunit;

namespace LogisticDesk.Tests;

public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    private readonly ApiFactory _factory;

    public ApiIntegrationTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var email = $"user{Guid.NewGuid():N}@example.com";
        const string password = "Str0ngPassw0rd";

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test",
            lastName = "User",
            email,
            password,
            confirmPassword = password,
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        Assert.False(string.IsNullOrEmpty(token.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    [Theory]
    [InlineData("/api/Vehicle")]
    [InlineData("/api/Shipment")]
    [InlineData("/api/auth/me")]
    public async Task Protected_endpoints_require_a_token(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var client = await AuthenticatedClient();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        var response = await client.GetAsync("/api/Vehicle");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401_problem_details()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "Wrong123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Json);
        Assert.Equal(401, problem!.Status);
        Assert.Equal("Invalid email or password.", problem.Title);
    }

    [Fact]
    public async Task Me_returns_the_logged_in_user()
    {
        var client = await AuthenticatedClient();
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", Json);
        Assert.Equal("Test", me!.FirstName);
    }

    [Fact]
    public async Task Full_workflow_assign_then_deliver_frees_the_vehicle()
    {
        var client = await AuthenticatedClient();

        var vehicleResponse = await client.PostAsJsonAsync("/api/Vehicle", new { plateNumber = "34 LOG 34", maxCapacity = 500 });
        var vehicle = (await vehicleResponse.Content.ReadFromJsonAsync<Vehicle>(Json))!;

        var assign = await client.PostAsJsonAsync("/api/Shipment/assign-direct", new { weight = 120, receiverAddress = "Bursa", vehicleId = vehicle.Id });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var shipment = (await assign.Content.ReadFromJsonAsync<Shipment>(Json))!;
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);

        var deliver = await client.PutAsync($"/api/Shipment/{shipment.Id}/deliver", null);
        Assert.Equal(HttpStatusCode.OK, deliver.StatusCode);

        var freed = await client.GetFromJsonAsync<Vehicle>($"/api/Vehicle/{vehicle.Id}", Json);
        Assert.Equal(VehicleStatus.Available, freed!.Status);

        var again = await client.PutAsync($"/api/Shipment/{shipment.Id}/deliver", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Invalid_page_size_returns_validation_problem()
    {
        var client = await AuthenticatedClient();
        var response = await client.GetAsync("/api/Vehicle?pageSize=1000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_vehicle_returns_404_problem_details()
    {
        var client = await AuthenticatedClient();
        var response = await client.GetAsync("/api/Vehicle/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Json);
        Assert.Equal(404, problem!.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Health_endpoints_are_public()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Swagger_marks_only_protected_operations_with_bearer_auth()
    {
        var doc = JsonDocument.Parse(await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json")).RootElement;
        var paths = doc.GetProperty("paths");

        Assert.False(paths.GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/Vehicle").GetProperty("get").TryGetProperty("security", out _));
        Assert.True(doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Business_rule_violation_returns_problem_details_with_reason()
    {
        var client = await AuthenticatedClient();
        var vehicle = (await (await client.PostAsJsonAsync("/api/Vehicle", new { plateNumber = "06 CAP 01", maxCapacity = 100 }))
            .Content.ReadFromJsonAsync<Vehicle>(Json))!;

        var response = await client.PostAsJsonAsync("/api/Shipment/assign-direct", new { weight = 250, receiverAddress = "Konya", vehicleId = vehicle.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(Json);
        Assert.Equal(400, problem!.Status);
        Assert.Contains("Capacity exceeded", problem.Detail);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
