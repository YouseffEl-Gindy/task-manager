import type { LoginRequest, RegisterRequest, UserDto } from "@/types/api";
import { requestJson, requestVoid } from "./client";

/** POST /auth/register -> 201 UserDto (+ session cookie). */
export function register(request: RegisterRequest): Promise<UserDto> {
  return requestJson<UserDto>("/auth/register", { method: "POST", body: request });
}

/** POST /auth/login -> 200 UserDto (+ session cookie). */
export function login(request: LoginRequest): Promise<UserDto> {
  return requestJson<UserDto>("/auth/login", { method: "POST", body: request });
}

/** POST /auth/logout -> 204 (cookie expired). Works with or without a session. */
export function logout(): Promise<void> {
  return requestVoid("/auth/logout", { method: "POST" });
}

/** GET /auth/me -> 200 UserDto, or 401 unauthorized. */
export function getCurrentUser(signal?: AbortSignal): Promise<UserDto> {
  return requestJson<UserDto>("/auth/me", { signal });
}
