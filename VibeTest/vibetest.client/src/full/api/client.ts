import { apiUrl } from '@/config/env';
import { ApiError, readErrorMessage } from '@/full/api/errors';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

interface ApiClientState {
  accessToken: string | null;
  refreshHandler: (() => Promise<string | null>) | null;
  logoutHandler: (() => void) | null;
}

const state: ApiClientState = {
  accessToken: null,
  refreshHandler: null,
  logoutHandler: null,
};

export function setAccessToken(token: string | null): void {
  state.accessToken = token;
}

export function getAccessToken(): string | null {
  return state.accessToken;
}

export function configureApiClient(handlers: {
  onRefresh: () => Promise<string | null>;
  onLogout: () => void;
}): void {
  state.refreshHandler = handlers.onRefresh;
  state.logoutHandler = handlers.onLogout;
}

async function request<T>(
  method: HttpMethod,
  path: string,
  body?: unknown,
  options?: { skipAuthRetry?: boolean },
): Promise<T> {
  const headers = new Headers({ Accept: 'application/json' });
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }
  if (state.accessToken) {
    headers.set('Authorization', `Bearer ${state.accessToken}`);
  }

  const init: RequestInit = { method, headers };
  if (body !== undefined) {
    init.body = JSON.stringify(body);
  }

  let response = await fetch(`${apiUrl}${path}`, init);

  if (response.status === 401 && !options?.skipAuthRetry && state.refreshHandler) {
    const newToken = await state.refreshHandler();
    if (newToken) {
      state.accessToken = newToken;
      headers.set('Authorization', `Bearer ${newToken}`);
      response = await fetch(`${apiUrl}${path}`, { ...init, headers });
    } else {
      state.logoutHandler?.();
    }
  }

  if (!response.ok) {
    if (response.status === 401) {
      state.logoutHandler?.();
    }
    throw new ApiError(response.status, await readErrorMessage(response));
  }

  const responseText = await response.text();

  if (response.status === 204 || responseText.length === 0) {
    return undefined as T;
  }

  return JSON.parse(responseText) as T;
}

export const apiClient = {
  get: <T>(path: string, options?: { skipAuthRetry?: boolean }) =>
    request<T>('GET', path, undefined, options),
  post: <T>(path: string, body?: unknown, options?: { skipAuthRetry?: boolean }) =>
    request<T>('POST', path, body, options),
  put: <T>(path: string, body?: unknown, options?: { skipAuthRetry?: boolean }) =>
    request<T>('PUT', path, body, options),
  patch: <T>(path: string, body?: unknown, options?: { skipAuthRetry?: boolean }) =>
    request<T>('PATCH', path, body, options),
  delete: (path: string, options?: { skipAuthRetry?: boolean }) =>
    request<void>('DELETE', path, undefined, options),
};
