/**
 * Configuration for visitor engagement offcanvas system.
 * All values are server-side configurable via appsettings.json.
 */
export interface EngagementConfig {
  /** Master switch for engagement system */
  enabled: boolean;

  /** Page count at which to first show engagement offcanvas (default: 2) */
  firstShowPageCount: number;

  /** How often to repeat showing offcanvas after first show (default: 5) */
  repeatInterval: number;

  /** How long dismissal lasts in current session, in minutes (default: 60 = 1 hour) */
  sessionDismissalTimeout: number;

  /** HTML messages for each engagement level (configured server-side) */
  messages: {
    /** Level 1: Awareness - shown at page 2 */
    level1: string;
    /** Level 2: Consideration - shown at page 7 */
    level2: string;
    /** Level 3: Conversion - shown at page 12+ */
    level3: string;
  };

  /** Call-to-action URLs */
  ctaUrls: {
    register: string;
    subscribe: string;
    features: string;
  };
}

/**
 * Default engagement configuration (fallback if server doesn't provide config)
 */
export const defaultEngagementConfig: EngagementConfig = {
  enabled: false,
  firstShowPageCount: 2,
  repeatInterval: 5,
  sessionDismissalTimeout: 60,
  messages: {
    level1:
      "<h4>Welcome to music4dance!</h4><p>The core service is <strong>free</strong>, but costs money to run. Create a <strong>free account</strong> to save your searches and build playlists.</p>",
    level2:
      "<h4>Still exploring?</h4><p>If you find this site useful, please consider <strong>subscribing</strong> to help keep it running. Your contribution covers hosting and gives you access to <a href='https://music4dance.blog/music4dance-help/subscriptions/'>exclusive features</a>.</p>",
    level3:
      "<h4>Thank you for using music4dance!</h4><p>This site is supported by user subscriptions. If you find it valuable, please <strong>consider subscribing</strong> to help cover hosting costs and keep it running for everyone.</p>",
  },
  ctaUrls: {
    register: "/identity/account/register",
    subscribe: "/home/contribute",
    features: "https://music4dance.blog/music4dance-help/subscriptions/",
  },
};
