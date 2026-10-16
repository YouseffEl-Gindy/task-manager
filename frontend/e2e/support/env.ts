import os from "node:os";
import path from "node:path";

/** Ports that don't clash with normal local dev (backend 5000, frontend 3000). */
export const BACKEND_PORT = 5100;
export const FRONTEND_PORT = 3100;

export const BACKEND_ORIGIN = `http://localhost:${BACKEND_PORT}`;
export const FRONTEND_ORIGIN = `http://localhost:${FRONTEND_PORT}`;

/**
 * Throwaway SQLite file for the e2e backend, in the OS temp folder. It is never the dev
 * database (backend/src/TaskManager.Api/taskmanager.db). The backend's webServer command
 * deletes it (and its -wal/-shm files) right before `dotnet run`, so every run starts empty.
 */
export const E2E_DB_PATH = path.join(os.tmpdir(), "task-manager-e2e", "e2e.db");

/** The browser's time zone in every test: UTC+05:30 with no daylight saving, so local <-> UTC is predictable. */
export const E2E_TIME_ZONE = "Asia/Kolkata";
