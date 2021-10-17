import { DanceEnvironment } from "./DanceEnvironmet";

declare const environment: DanceEnvironment;

interface Link {
  text: string;
  url: string;
}

export class DanceText {
  constructor(private description: string) {}

  public expanded(): string {
    let description = this.description;
    if (!description) {
      return "<p>We're busy doing research and pulling together a general description for @Model.DanceName dance.  Please check back later for more info.</p>";
    }
    description = description.replace("&lt;", "<");
    description = description.replace("&gt;", ">");

    const result = description.replaceAll(
      /\[(?<dance>[^\]]*)\](?<trailing>[^(])/g,
      (match, dance, trailing) => {
        const link = DanceText.FindLink(dance);
        return link ? `[${dance}](${link})${trailing}` : match;
      }
    );
    return result;
  }

  private static FindLink(dance: string): string | undefined {
    let link = DanceText.FindDance(dance);
    if (!link) {
      link = DanceText.FindCategory(dance);
    }
    if (!link) {
      link = DanceText.FindStaticLink(dance);
    }
    return link;
  }

  private static FindDance(dance: string): string | undefined {
    const ds = environment.fromName(dance);
    return ds ? `/dances/${ds.seoName}` : undefined;
  }

  private static FindCategory(category: string): string | undefined {
    const l = category.toLowerCase();
    const c = DanceText.categories.find((x) => x === l);
    return c ? `/dances/${l.replace(" ", "-")}` : undefined;
  }

  private static FindStaticLink(text: string): string | undefined {
    const t = text.toLowerCase();
    const link = DanceText.links.find((x) => x.text === t);
    return link ? link.url : undefined;
  }

  // TODO: Can we derive these from the dance stats DB?
  private static categories = [
    "international standard",
    "international latin",
    "american smooth",
    "american rhythm",
  ];

  private static links: Link[] = [
    { text: "swing music", url: "http://en.wikipedia.org/wiki/Swing_music" },
    { text: "tango music", url: "http://en.wikipedia.org/wiki/Tango" },
  ];
}
