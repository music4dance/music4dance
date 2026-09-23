/// <reference types="vite/client" />

// Google AdSense pushes its config queue onto window as `adsbygoogle`; setting
// `pauseAdRequests` to 1 holds new ad requests back (0 resumes them).
interface AdsByGoogle extends Array<Record<string, unknown>> {
  pauseAdRequests?: number;
}

interface Window {
  adsbygoogle?: AdsByGoogle;
}
