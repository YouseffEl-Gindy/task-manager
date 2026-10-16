import type { Metadata } from "next";
import { EditTaskScreen } from "@/components/tasks/EditTaskScreen";

export const metadata: Metadata = { title: "Edit task" };

export default async function EditTaskPage({ params }: PageProps<"/tasks/[id]">) {
  const { id } = await params;
  return <EditTaskScreen id={id} />;
}
