import type { Metadata } from "next";
import { ChangePasswordForm } from "@/components/account/ChangePasswordForm";
import { DeleteAccountSection } from "@/components/account/DeleteAccountSection";
import { ProfileForm } from "@/components/account/ProfileForm";

export const metadata: Metadata = { title: "Account" };

export default function AccountPage() {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-bold text-slate-900">Account</h1>
      <div className="rounded-lg border border-slate-200 bg-white p-4 sm:p-6">
        <ProfileForm />
      </div>
      <div className="rounded-lg border border-slate-200 bg-white p-4 sm:p-6">
        <ChangePasswordForm />
      </div>
      <div className="rounded-lg border border-red-200 bg-white p-4 sm:p-6">
        <DeleteAccountSection />
      </div>
    </div>
  );
}
