![Status](https://img.shields.io/badge/Status-Active_Development-brightgreen?style=for-the-badge) ![.NET](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
[![CI](https://github.com/bcpasaoglu/Logistic-Workflow-API/actions/workflows/ci.yml/badge.svg)](https://github.com/bcpasaoglu/Logistic-Workflow-API/actions/workflows/ci.yml)

# Project Purpose

This project is a Logistics Workflow API project that I developed from scratch in order to **apply, test, and reinforce what I have learned** while taking my first steps into the .NET ecosystem and backend development.

Rather than creating only a simple "add/delete data" CRUD application, I aimed to implement real-world business rules in code and establish interconnected database relationships.

---

## What Did I Learn and Apply in This Project?

Throughout the development process, I researched and solved the problems I encountered, and I had the opportunity to practise the following topics:

* **Entity Framework Core & MySQL Integration:** I created database tables through C# code using the Code-First approach and learned the logic of `Migration`.
* **Business Logic Implementation:**

  * While assigning a shipment to a vehicle, I wrote validation algorithms that check the vehicle's **availability status** and **maximum carrying capacity**.
  * I ensured that the system rejects the request if the capacity is exceeded, and automatically updates the vehicle status to `OnRoute` if all conditions are met.
* **Flexible API Design:** To simulate real-life processes, I designed flexible Controller methods that support different scenarios, such as assigning shipments directly to a vehicle or storing them in the warehouse first and loading them onto a vehicle later.
* **Error Resolution and Architectural Configuration:**

  * While retrieving relational data, such as the vehicle inside a shipment or the shipments inside a vehicle, I learned how to solve the **Object Cycle** error by using `ReferenceHandler.IgnoreCycles`.
  * In order to connect the project to a frontend application in the future, I researched, learned about, and successfully configured **CORS policies** that allow requests from external applications.

---

## Technologies Used

* **Language:** C#, .NET 9, ASP.NET Core Web API
* **Database & ORM:** MySQL, Entity Framework Core
* **Testing Tool:** Swagger
* **Architectural Concepts:** Asynchronous Programming (`async/await`), RESTful API Design

---

## Main Endpoints Developed

The project contains two main management modules: `Vehicle` and `Shipment`.

In addition to standard CRUD operations such as adding, deleting, and updating vehicles, I implemented the main business logic through the following methods:

* `POST /api/Shipment/warehouse` -> Stores the shipment only in the warehouse without assigning it to a vehicle.
* `POST /api/Shipment/assign-direct` -> Adds the shipment directly to the system and loads it onto a vehicle after performing capacity checks.
* `PUT /api/Shipment/{shipmentId}/load-to-vehicle/{vehicleId}` -> Takes a shipment waiting in the warehouse and assigns it to an available vehicle.

---

## Running with Docker

```bash
docker compose up --build
# API + Swagger UI: http://localhost:8080/swagger
```

`docker-compose.yml` starts MySQL 8.4 and the API. The API applies EF Core migrations on startup (`Database__MigrateOnStartup=true`), so no manual database setup is needed.

## Running Locally

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=logisticdesk;User=...;Password=...;"
dotnet ef database update
dotnet run
```

## Tests

```bash
dotnet test LogisticDesk.slnx
```

The `tests/LogisticDesk.Tests` project (xUnit + EF Core InMemory) covers the assignment rules and the shipment endpoints:

* capacity limits, including a shipment exactly at capacity
* rejecting vehicles that are on route or in maintenance
* warehouse → vehicle loading and the resulting status changes
* updates cannot bypass the capacity checks by changing the vehicle or raising the weight

GitHub Actions builds the solution, runs the tests and builds the Docker image on every push and pull request.

## Project Structure

```
Controllers/        Auth, Shipment and Vehicle endpoints
Services/           ShipmentAssignmentRules: capacity and availability rules, kept free of EF/HTTP
DTOs/               Request models with validation attributes
Models/, Enums/     EF Core entities and status enums
Data/, Migrations/  DbContext and Code-First migrations
tests/              xUnit test project
```

---

## Development Process & Roadmap

This project is being developed to improve my competence in .NET and backend architectures and to test theoretical knowledge through real-world scenarios. I am tracking the current status of the project and my future goals step by step through the following list:

### ✅ Completed Steps (Current Infrastructure)

* [x] **Infrastructure and Database Setup:** Building the .NET 9 Web API and configuring the MySQL database connection.
* [x] **Relational Data Model:** Designing the `Vehicle` and `Shipment` models and configuring the one-to-many relationship between them in the database.
* [x] **CRUD Configuration:** Completing the API endpoints that manage vehicle creation, updating, []deletion, and filtering by status.
* [x] **Logistics Business Logic:** Writing validation algorithms that check the vehicle's availability status and maximum carrying capacity (`MaxCapacity`) during the shipment assignment process.
* [x] **Flexible Process Modelling:** Configuring methods that support scenarios where shipments are either stored in the warehouse first or directly loaded onto a vehicle.
* [x] **Data Configuration:** Configuring decimal values such as capacity and weight to be stored with exactly two digits after the decimal point at the database level.
* [x] **Error Resolution:** Permanently resolving the Object Cycle error that occurred during the JSON serialization of relational data by using `ReferenceHandler.IgnoreCycles`. // Error fix
* [x] **CORS Policy Configuration:** Successfully configuring CORS policies so that frontend applications can communicate with the API securely and without problems.
