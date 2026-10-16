/**
 * Deletes the throwaway e2e SQLite database (plus -wal/-shm/-journal) and makes sure its folder exists.
 * Run by the backend webServer command in playwright.config.ts right before `dotnet run`, because
 * Playwright starts webServers BEFORE globalSetup, and the backend opens the file as soon as it starts.
 *
 * Usage: node e2e/support/reset-e2e-db.mts <absolute path to e2e.db>
 */
import { mkdirSync, rmSync } from "node:fs";
import path from "node:path";

const dbPath = process.argv[2];
if (!dbPath || !path.isAbsolute(dbPath) || path.basename(dbPath) !== "e2e.db") {
  console.error(`reset-e2e-db: expected an absolute path to e2e.db, got "${dbPath ?? ""}"`);
  process.exit(1);
}

mkdirSync(path.dirname(dbPath), { recursive: true });
for (const suffix of ["", "-wal", "-shm", "-journal"]) {
  rmSync(`${dbPath}${suffix}`, { force: true });
}
console.log(`reset-e2e-db: fresh database at ${dbPath}`);
