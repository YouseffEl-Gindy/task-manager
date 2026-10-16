import { render, type RenderResult } from "@testing-library/react";
import type { ReactElement } from "react";
import type { UserDto } from "@/types/api";
import { AuthProvider } from "@/lib/auth/AuthContext";

/** Renders inside an AuthProvider, optionally with a signed-in user. */
export function renderWithAuth(ui: ReactElement, options: { user?: UserDto | null } = {}): RenderResult {
  return render(<AuthProvider initialUser={options.user ?? null}>{ui}</AuthProvider>);
}
