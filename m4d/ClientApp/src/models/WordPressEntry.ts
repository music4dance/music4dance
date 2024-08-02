import { type SearchPage } from "./SearchPage";

export interface WordPressEntry {
  id: number;
  date: string;
  slug: string;
  type: string;
  link: string;
  title: WordPressString;
  content: WordPressString;
  excerpt: WordPressString;
}

export interface WordPressString {
  rendered: string;
}

export function searchPageFromWordPress(entry: WordPressEntry): SearchPage {
  const ellip = "&hellip";
  let description = entry.excerpt.rendered;
  const truncate = description.indexOf(ellip);
  description = truncate > 0 ? description.substring(0, truncate + ellip.length + 1) : description;
  return {
    date: entry.date,
    url: entry.link,
    title: entry.title.rendered,
    description: description,
  };
}
