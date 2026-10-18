import { HttpErrorResponse } from '@angular/common/http';

/** What the API's problem-details JSON looks like (Day 9 gave every error this shape plus a correlation id). */
interface ProblemBody {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
  correlationId?: string;
}

/** A failed API call turned into something a screen can show. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly fieldErrors: Record<string, string[]> = {},
    public readonly correlationId: string | null = null
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

function friendly(status: number): string {
  switch (status) {
    case 0: return "Can't reach the server. Check your connection and that the API is running.";
    case 401: return 'Your session has ended. Sign in again.';
    case 403: return "You don't have permission to do this.";
    case 404: return "That item doesn't exist (any more).";
    case 409: return 'That conflicts with something that already exists.';
    case 429: return 'Too many attempts. Wait a minute and try again.';
    default: return status >= 500 ? 'The server hit a problem. Try again in a moment.' : 'The request was not accepted.';
  }
}

export function toApiError(error: HttpErrorResponse): ApiError {
  const body: ProblemBody = typeof error.error === 'object' && error.error !== null ? (error.error as ProblemBody) : {};
  const fieldErrors = body.errors ?? {};
  const firstField = Object.values(fieldErrors).flat()[0];
  // For 5xx the detail can hold internal text in Development; show something calm instead.
  const message = error.status >= 500 || error.status === 0
    ? friendly(error.status)
    : body.detail || firstField || (error.status === 401 || error.status === 403 || error.status === 429 ? friendly(error.status) : body.title) || friendly(error.status);
  return new ApiError(error.status, message, fieldErrors, body.correlationId ?? null);
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Something went wrong.';
}
