import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { resetNavigation } from "./mocks/nextNavigation";

// Mocks declared in a setup file apply to every test file.
vi.mock("next/navigation", () => import("./mocks/nextNavigation"));
vi.mock("next/link", () => import("./mocks/nextLink"));

afterEach(() => {
  cleanup();
  resetNavigation();
  window.history.replaceState(null, "", "/");
});
