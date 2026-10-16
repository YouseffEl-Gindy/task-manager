import type { Metadata } from "next";
import { Suspense } from "react";
import { TasksScreen } from "@/components/tasks/TasksScreen";
import { Spinner } from "@/components/ui/Spinner";

export const metadata: Metadata = { title: "Tasks" };

export default function TasksPage() {
  return (
    // TasksScreen reads the filter state with useSearchParams, which needs a Suspense boundary.
    <Suspense fallback={<Spinner label="Loading tasks…" />}>
      <TasksScreen />
    </Suspense>
  );
}
