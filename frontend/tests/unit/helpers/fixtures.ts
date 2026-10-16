import type { TaskDto, UserDto } from "@/types/api";

export const USER: UserDto = {
  id: "8d0f5a52-2c1b-4f4e-9a57-0b6c2f1e9d31",
  email: "ada@example.com",
  displayName: "Ada Lovelace",
  createdAt: "2026-10-10T14:30:00.1234567Z",
};

let counter = 0;

export function makeTask(overrides: Partial<TaskDto> = {}): TaskDto {
  counter += 1;
  return {
    id: `00000000-0000-4000-8000-${String(counter).padStart(12, "0")}`,
    title: `Task ${counter}`,
    description: null,
    status: "todo",
    priority: "medium",
    dueDate: null,
    createdAt: "2026-10-10T14:35:12.441Z",
    updatedAt: "2026-10-10T14:35:12.441Z",
    ...overrides,
  };
}
