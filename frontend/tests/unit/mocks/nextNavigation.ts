import { vi } from "vitest";

function createRouter() {
  return {
    push: vi.fn<(href: string, options?: { scroll?: boolean }) => void>(),
    replace: vi.fn<(href: string, options?: { scroll?: boolean }) => void>(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn(),
  };
}

/** Mutable navigation state the tests control. Objects stay stable within a test. */
export const navigation = {
  router: createRouter(),
  searchParams: new URLSearchParams(),
  pathname: "/",
};

export function resetNavigation(): void {
  navigation.router = createRouter();
  navigation.searchParams = new URLSearchParams();
  navigation.pathname = "/";
}

export function setSearchParams(query: string): void {
  navigation.searchParams = new URLSearchParams(query);
}

export function useRouter() {
  return navigation.router;
}

export function useSearchParams() {
  return navigation.searchParams;
}

export function usePathname() {
  return navigation.pathname;
}
