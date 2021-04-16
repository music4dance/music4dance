export interface Editor {
  readonly isModified: boolean;
  commit(): void;
}
