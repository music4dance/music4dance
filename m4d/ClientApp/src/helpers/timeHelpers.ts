import { format } from "date-fns";

export function timeOrder(time: Date): string {
  if (!time) {
    return "U";
  }

  const delta = (Date.now() - time.valueOf()) / 1000;

  if (delta < 60) {
    return "s";
  }
  if (delta < 60 * 60) {
    return "m";
  }
  if (delta < 60 * 60 * 24) {
    return "h";
  }
  if (delta < 60 * 60 * 24 * 7) {
    return "D";
  }
  if (delta < 60 * 60 * 24 * 30) {
    return "W";
  }
  return delta < 60 * 60 * 24 * 365 ? "M" : "Y";
}

export function timeOrderVerbose(time: Date): string {
  switch (timeOrder(time)) {
    case "s":
      return "seconds";
    case "m":
      return "minutes";
    case "h":
      return "hours";
    case "D":
      return "days";
    case "W":
      return "weeks";
    case "M":
      return "months";
    default:
      return "years";
  }
}

export function formatDate(date: Date): string {
  return format(date, "dd-MMM-yyyy hh:mm:ss a");
}

export function formatNow(): string {
  return formatDate(new Date());
}
