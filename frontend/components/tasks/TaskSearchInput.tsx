"use client";

import { useEffect, useState } from "react";
import { SEARCH_MAX_LENGTH } from "@/lib/tasks/queryParams";
import { TextField } from "@/components/ui/TextField";

export const SEARCH_DEBOUNCE_MS = 300;

export interface TaskSearchInputProps {
  /** The committed search from the URL ("" when none). */
  value: string;
  /** Called with the trimmed text 300 ms after the user stops typing. */
  onSearchChange: (search: string) => void;
}

export function TaskSearchInput({ value, onSearchChange }: TaskSearchInputProps) {
  const [text, setText] = useState(value);
  const [lastValue, setLastValue] = useState(value);

  // Follow external URL changes (e.g. "Clear filters") without clobbering what the user is typing.
  if (value !== lastValue) {
    setLastValue(value);
    if (value !== text.trim()) {
      setText(value);
    }
  }

  useEffect(() => {
    const trimmed = text.trim();
    if (trimmed === value) {
      return;
    }
    const timer = setTimeout(() => onSearchChange(trimmed), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [text, value, onSearchChange]);

  return (
    <TextField
      label="Search"
      type="search"
      name="search"
      placeholder="Search title and description"
      maxLength={SEARCH_MAX_LENGTH}
      value={text}
      onChange={(e) => setText(e.target.value)}
    />
  );
}
