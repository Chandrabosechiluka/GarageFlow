import { HttpErrorResponse } from '@angular/common/http';
import { errorMessage, toApiError } from './api-error';

describe('api errors', () => {
  it('uses the detail from a problem-details body and keeps the correlation id', () => {
    const e = toApiError(new HttpErrorResponse({ status: 409, error: { detail: 'That time was just taken.', correlationId: 'abc' } }));
    expect(e.message).toBe('That time was just taken.');
    expect(e.correlationId).toBe('abc');
  });

  it('falls back to the first field error', () => {
    const e = toApiError(new HttpErrorResponse({ status: 400, error: { errors: { Email: ['Email is not valid.'] } } }));
    expect(e.message).toBe('Email is not valid.');
  });

  it('does not show server internals for 5xx', () => {
    const e = toApiError(new HttpErrorResponse({ status: 500, error: { detail: 'NullReferenceException at ...' } }));
    expect(e.message).not.toContain('NullReference');
  });

  it('explains an unreachable server', () => {
    expect(toApiError(new HttpErrorResponse({ status: 0 })).message).toContain("Can't reach the server");
  });

  it('turns unknown values into a generic message', () => {
    expect(errorMessage('boom')).toBe('Something went wrong.');
  });
});
