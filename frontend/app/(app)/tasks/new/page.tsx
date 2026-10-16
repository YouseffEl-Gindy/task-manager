import type { Metadata } from "next";
import { NewTaskScreen } from "@/components/tasks/NewTaskScreen";

export const metadata: Metadata = { title: "New task" };

export default function NewTaskPage() {
  return <NewTaskScreen />;
}
