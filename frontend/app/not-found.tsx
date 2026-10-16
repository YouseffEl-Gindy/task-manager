import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";

export default function NotFound() {
  return (
    <main id="main-content" className="mx-auto flex w-full max-w-xl flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-2xl font-bold text-slate-900">Page not found</h1>
      <p className="text-slate-700">The page you are looking for doesn&apos;t exist.</p>
      <Link href="/" className={buttonClassName("primary")}>
        Go to the home page
      </Link>
    </main>
  );
}
