/**
 * Turns an API error body into one readable sentence.
 * Handles RFC 7807 ProblemDetails (business rules, 404/409),
 * ValidationProblemDetails ({ errors: { Field: [..] } }) and the
 * Identity register error ({ message, errors: [{ description }] }).
 */
export function describeProblem(body: unknown, status: number): string {
  if (body && typeof body === 'object') {
    const b = body as Record<string, unknown>;

    if (Array.isArray(b.errors)) {
      const parts = b.errors
        .map((e) => (e && typeof e === 'object' ? (e as Record<string, unknown>).description : undefined))
        .filter((d): d is string => typeof d === 'string');
      if (parts.length) return parts.join(' ');
    }

    if (b.errors && typeof b.errors === 'object') {
      const messages = Object.values(b.errors as Record<string, unknown>)
        .flatMap((v) => (Array.isArray(v) ? v : [v]))
        .filter((m): m is string => typeof m === 'string');
      if (messages.length) return messages.join(' ');
    }

    if (typeof b.detail === 'string' && b.detail) return b.detail;
    if (typeof b.title === 'string' && b.title) return b.title;
    if (typeof b.message === 'string' && b.message) return b.message;
  }

  if (typeof body === 'string' && body) return body;
  return status === 401 ? 'Your session has expired. Please sign in again.' : `Request failed (${status}).`;
}
