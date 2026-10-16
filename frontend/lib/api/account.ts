import type {
  ChangePasswordRequest,
  DeleteAccountRequest,
  UpdateProfileRequest,
  UserDto,
} from "@/types/api";
import { requestJson, requestVoid } from "./client";

/** PUT /account/profile -> 200 UserDto. */
export function updateProfile(request: UpdateProfileRequest): Promise<UserDto> {
  return requestJson<UserDto>("/account/profile", { method: "PUT", body: request });
}

/** POST /account/change-password -> 204. A wrong current password is 400 (key currentPassword). */
export function changePassword(request: ChangePasswordRequest): Promise<void> {
  return requestVoid("/account/change-password", { method: "POST", body: request });
}

/** POST /account/delete -> 204 (cookie expired). A wrong password is 400 (key password). */
export function deleteAccount(request: DeleteAccountRequest): Promise<void> {
  return requestVoid("/account/delete", { method: "POST", body: request });
}
