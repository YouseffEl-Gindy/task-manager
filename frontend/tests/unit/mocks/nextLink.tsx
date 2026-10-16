import type { AnchorHTMLAttributes, ReactNode } from "react";

type LinkProps = Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "href"> & {
  href: string;
  children?: ReactNode;
  prefetch?: boolean | null;
  replace?: boolean;
  scroll?: boolean;
};

/** A plain anchor stand-in for next/link in unit tests. */
export default function Link({ href, children, prefetch, replace, scroll, ...rest }: LinkProps) {
  void prefetch;
  void replace;
  void scroll;
  return (
    <a href={href} {...rest}>
      {children}
    </a>
  );
}
