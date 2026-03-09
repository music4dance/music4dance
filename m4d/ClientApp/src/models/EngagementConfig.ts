/**
 * Configuration for visitor engagement offcanvas system.
 * All values are server-side configurable via appsettings.json.
 */
export interface EngagementConfig {
  /** Master switch for engagement system */
  enabled: boolean;

  /** Show engagement UI for anonymous users */
  showForAnonymous?: boolean;

  /** Show engagement UI for logged-in non-premium users */
  showForLoggedIn?: boolean;

  /** Page count at which to first show engagement offcanvas (default: 2) */
  firstShowPageCount: number;

  /** How often to repeat showing offcanvas after first show (default: 5) */
  repeatInterval: number;

  /** How long dismissal lasts in current session, in minutes (default: 60 = 1 hour) */
  sessionDismissalTimeout: number;

  /** HTML messages for each engagement level (configured server-side) */
  messages: {
    /** Level 1: Awareness - shown at page 2 (anonymous users) */
    level1: string;
    /** Level 2: Consideration - shown at page 7 (anonymous users) */
    level2: string;
    /** Level 3: Conversion - shown at page 12+ (anonymous users) */
    level3: string;
    /** Logged-in non-premium users message */
    loggedInUpgrade?: string;
  };

  /** Premium membership benefits (for logged-in users) */
  premiumBenefits?: {
    /** List of premium feature items */
    items: string[];
    /** Text for "more features" indicator */
    moreText?: string;
    /** URL to complete features list */
    completeListUrl: string;
  };

  /** Call-to-action URLs */
  ctaUrls: {
    register: string;
    login?: string;
    subscribe: string;
    features: string;
  };
}

/**
 * Default engagement configuration (fallback if server doesn't provide config)
 */
export const defaultEngagementConfig: EngagementConfig = {
  enabled: false,
  showForAnonymous: true,
  showForLoggedIn: true,
  firstShowPageCount: 2,
  repeatInterval: 5,
  sessionDismissalTimeout: 60,
  messages: {
    level1:
      "<p>Exploring music4dance? We're glad you're here! Create a <strong>free account</strong> to unlock helpful features that make finding dance music even easier.</p>",
    level2:
      "<p>Still searching for music? We've noticed you're using the site quite a bit. Creating a <strong>free account</strong> unlocks features like saving searches, tagging songs, and customizing your experience. It only takes a minute!</p>",
    level3:
      "<p>You're clearly finding music4dance useful for your dance music needs! Create a <strong>free account</strong> to get the most out of the platform. You'll be able to tag songs, save your favorite searches, and build your perfect dance music collection.</p>",
    loggedInUpgrade:
      "<p>Upgrade to Premium membership to unlock advanced features and support the music4dance community.</p>",
  },
  premiumBenefits: {
    items: [
      "<a href='https://music4dance.blog/music4dance-help/advanced-search/' target='_blank'>Advanced search filters</a>",
      "<a href='https://music4dance.blog/music4dance-help/playing-or-purchasing-songs/spotify-playlist/' target='_blank'>Spotify playlist integration</a>",
      "<a href='https://music4dance.blog/music4dance-help/bonus-content/' target='_blank'>Bonus content access</a>",
      "Custom dance categories",
      "Priority email support",
      "Ad-free experience",
    ],
    moreText: "...and more!",
    completeListUrl: "https://music4dance.blog/music4dance-help/subscriptions/",
  },
  ctaUrls: {
    register: "/identity/account/register",
    login: "/identity/account/login",
    subscribe: "/home/contribute",
    features: "https://music4dance.blog/music4dance-help/subscriptions/",
  },
};
