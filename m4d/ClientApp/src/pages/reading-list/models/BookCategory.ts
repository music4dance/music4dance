import type { Book } from "./BookModel";
import { type BookSelector, BookType } from "./BookType";

export class BookCategory {
  public books: Book[];

  constructor(
    public type: BookType,
    public title: string,
    public description: string,
    books: Book[],
  ) {
    this.books = BookCategory.extractCategory(books, type);
  }

  private static extractCategory(books: Book[], type: BookType): Book[] {
    return books.filter((b) => BookCategory.selectorContains(b.type, type));
  }

  private static selectorContains(selector: BookSelector, type: BookType): boolean {
    if (Array.isArray(selector)) {
      const rg = selector as BookType[];
      const idx = rg.findIndex((t) => t === type);
      return idx != -1;
    } else {
      const stype = selector as BookType;
      return stype == type;
    }
  }
}
