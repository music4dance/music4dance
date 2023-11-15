import { type BookSelector } from "./BookType";

export interface Book {
  type: BookSelector;
  image: string;
  title: string;
  subtitle?: string;
  author: string;
  kindle?: string;
  paperback?: string;
  hardcover?: string;
  audible?: string;
  others?: Link[];
  review?: string;
  notes?: string;
  website?: Link;
}

export interface Link {
  text: string;
  ref: string;
}
