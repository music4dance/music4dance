export interface BreadCrumbItem {
  text: string;
  href?: string;
  active?: boolean;
}

export const homeCrumb: BreadCrumbItem = { text: "music4dance", href: "/" };

export const danceCrumb: BreadCrumbItem = { text: "Dances", href: "/dances" };

export const songCrumb: BreadCrumbItem = {
  text: "Song Library",
  href: "/song",
};

export const ballroomCrumb: BreadCrumbItem = {
  text: "Ballroom",
  href: "/dances/ballroom-competition-categories",
};

export const infoCrumb: BreadCrumbItem = { text: "Info", href: "/home/info" };

export const danceTrail: BreadCrumbItem[] = [homeCrumb, danceCrumb];

export const ballroomTrail: BreadCrumbItem[] = [
  homeCrumb,
  danceCrumb,
  ballroomCrumb,
];

export const infoTrail: BreadCrumbItem[] = [homeCrumb, infoCrumb];
