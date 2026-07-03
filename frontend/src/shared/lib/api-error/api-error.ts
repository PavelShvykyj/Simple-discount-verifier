export interface ApiError {
  readonly code?: string;
  readonly Code?: string;
  readonly message?: string;
  readonly Message?: string;
  readonly correlationId?: string | null;
  readonly CorrelationId?: string | null;
}

export interface ApiErrorResponse {
  readonly error?: ApiError;
  readonly Error?: ApiError;
}

export type ApiErrorMessage = string | ApiError | ApiErrorResponse;

export function toApiErrorDisplayMessage(
  value: ApiErrorMessage,
  fallbackMessage: string,
): string {
  if (typeof value === 'string') {
    return value;
  }

  const apiError = extractApiError(value);
  const message = normalizeText(apiError?.message ?? apiError?.Message) ?? fallbackMessage;
  const correlationId = normalizeText(apiError?.correlationId ?? apiError?.CorrelationId);

  if (correlationId === null) {
    return message;
  }

  return `${message}\nКод звернення: ${correlationId}`;
}

function extractApiError(value: ApiError | ApiErrorResponse): ApiError | null {
  if ('error' in value) {
    return value.error ?? null;
  }

  if ('Error' in value) {
    return value.Error ?? null;
  }

  return isApiError(value) ? value : null;
}

function isApiError(value: ApiError | ApiErrorResponse): value is ApiError {
  return (
    'code' in value ||
    'Code' in value ||
    'message' in value ||
    'Message' in value ||
    'correlationId' in value ||
    'CorrelationId' in value
  );
}

function normalizeText(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const trimmedValue = value.trim();

  return trimmedValue.length > 0 ? trimmedValue : null;
}
