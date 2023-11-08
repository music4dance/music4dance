export class UserQuery {
  namePseudo = "|p";
  queryPseudo = "[p]";

  public static fromParts(parts?: string, user?: string): UserQuery {
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
      case "D":
        suffix = "d";
        break;
      case "L":
        suffix = "l";
        break;
      case "H":
        suffix = "h";
        break;
      case "T":
        suffix = "a";
        break;
      case "X":
        suffix = "x";
        break;
      default:
        throw new Error(
          '2nd character of parts must be one of "L", "H", or "T"'
        );
    }

    return new UserQuery(`${prefix}${user ?? "me"}|${suffix}`);
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
    return this.data
      .substring(1, idx === -1 ? undefined : idx)
      .replace(this.queryPseudo, "");
  }

  public get isAnonymous(): boolean {
    const userName = this.userName;
    return userName.indexOf("-") !== -1 && userName.length === 36;
  }

  public get isPseudo(): boolean {
    return this.data.indexOf(this.queryPseudo) != -1;
  }

  public get isBatch(): boolean {
    const user = this.userName;
    return !!user && (user === "batch" || user.startsWith("batch-"));
  }

  public get displayName(): string {
    return this.isAnonymous ? "Anonymous" : this.userName;
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
    const user = this.displayName;
    const my = user === "me" ? "my" : user + "'s";
    switch (this.parts[1]) {
      case "L":
        like = `in ${my} favorites`;
        break;
      case "H":
        like = `in ${my} blocked list`;
        break;
      case "T":
        like = `edited by ${user}`;
        break;
      case "D":
        like = `voted for by ${user}`;
        break;
      case "X":
        like = `voted against by ${user}`;
        break;
    }

    return `${include} songs ${like}`;
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
        case "d":
          ret += "D";
          break;
        case "x":
          ret += "X";
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

    if (query.toLowerCase().endsWith(this.namePseudo)) {
      query =
        query.substring(0, query.length - this.namePseudo.length) +
        this.queryPseudo;
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
