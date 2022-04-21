import { BookSelector } from "./BookType";

export interface Book {
  type: BookSelector;
  image: string;
  title: string;
  subtitle?: string;
  author: string;
  kindle?: string;
  paperback?: string;
  hardcover?: string;
  others?: Link[];
  review?: string;
  notes?: string;
}

export interface Link {
  text: string;
  ref: string;
}
