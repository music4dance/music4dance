export class UserQuery {
  public static fromParts(parts?: string): UserQuery {
    if (!parts || parts === "NT") {
      return new UserQuery("");
    }

    if (parts.length !== 2) {
      throw new Error("parts should be exactly 2 characters");
    }

    let prefix = "";
    if (parts[0] === "I") {
      prefix = "+";
    } else if (parts[0] === "X") {
      prefix = "-";
    } else {
      throw new Error('1st character of parts must be "I" or "X"');
    }

    let suffix = "";
    switch (parts[1]) {
      case "L":
        suffix = "l";
        break;
      case "H":
        suffix = "h";
        break;
      case "T":
        suffix = "a";
        break;
      default:
        throw new Error(
          '2nd character of parts must be one of "L", "H", or "T"'
        );
    }

    return new UserQuery(prefix + "me|" + suffix);
  }

  private data: string;

  public constructor(query?: string) {
    this.data = this.normalize(query);
  }

  public get query(): string {
    return this.data;
  }

  public get isEmpty(): boolean {
    return !this.data;
  }

  public get include(): boolean {
    return this.data[0] === "+";
  }

  public get like(): boolean {
    return this.parts[1] === "L";
  }

  public get hate(): boolean {
    return this.parts[1] === "H";
  }

  public get tag(): boolean {
    return this.parts[1] === "T";
  }

  public get userName(): string {
    const idx = this.data.indexOf("|");
    return this.data.substring(1, idx);
  }

  public isDefault(userName?: string): boolean {
    if (!userName && !this.data) {
      return true;
    }
    const field = this.data?.toLowerCase();
    return field
      ? field === "-me|h" || field === `-${userName?.toLowerCase()}|h`
      : !field;
  }

  public get description(): string {
    if (this.isEmpty) {
      return "";
    }

    const include = this.include ? "including" : "excluding";
    let like = "";
    switch (this.parts[1]) {
      case "L":
        like = "liked";
        break;
      case "H":
        like = "disliked";
        break;
      case "T":
        like = "edited";
        break;
    }

    return `${include} songs ${like} by ${this.userName}`;
  }

  public get parts(): string {
    if (!this.data) {
      return "NT";
    }

    let ret = this.data[0] === "-" ? "X" : "I";

    const parts = this.data.split("|");
    if (parts.length > 1) {
      switch (parts[1]) {
        case "l":
          ret += "L";
          break;
        case "h":
          ret += "H";
          break;
        default:
          ret += "T";
      }
    } else {
      ret += "T";
    }

    return ret;
  }

  private normalize(query?: string): string {
    if (!query) {
      return "";
    }
    query = query.trim().toLowerCase();
    if (!query) {
      return "";
    }
    if (query === "null") {
      return query;
    }

    if (query[0] !== "+" && query[0] !== "-") {
      query = "+" + query;
    }
    if (query.indexOf("|") === -1) {
      query += "|";
    }
    return query;
  }
}
