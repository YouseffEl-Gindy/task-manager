/** A loading indicator with an accessible label. */
export function Spinner({ label = "Loading…" }: { label?: string }) {
  return (
    <div role="status" className="flex items-center justify-center gap-3 py-8 text-slate-600">
      <span
        aria-hidden="true"
        className="h-5 w-5 animate-spin rounded-full border-2 border-slate-300 border-t-blue-700"
      />
      <span className="text-sm">{label}</span>
    </div>
  );
}
