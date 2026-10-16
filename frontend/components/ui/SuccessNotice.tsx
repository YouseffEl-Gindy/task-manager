/** A short confirmation message, announced politely to screen readers. */
export function SuccessNotice({ message }: { message: string | null }) {
  return (
    <p role="status" className={message ? "rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800" : "sr-only"}>
      {message ?? ""}
    </p>
  );
}
