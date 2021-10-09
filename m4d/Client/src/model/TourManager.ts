import { jsonArrayMember, jsonMember, jsonObject, TypedJSON } from "typedjson";

const tourKey = "tour-status";

@jsonObject
export class TourStatus {
  @jsonMember public id!: string;
  @jsonMember public skipped?: boolean;
  @jsonMember public completed?: boolean;

  public get show(): boolean {
    return !this.skipped && !this.completed;
  }
}

@jsonObject
export class TourManager {
  @jsonMember public skipAll?: boolean;
  @jsonArrayMember(TourStatus) public tours!: TourStatus[];

  constructor() {
    this.skipAll = false;
    this.tours = [];
  }

  public static loadTours(): TourManager {
    const tourString = localStorage.getItem(tourKey);
    if (!tourString) {
      return new TourManager();
    }
    const manager = TypedJSON.parse(tourString, TourManager);
    return manager ? manager : new TourManager();
  }

  public showTour(id: string): boolean {
    if (this.skipAll) {
      return false;
    }
    const tour = this.findTour(id);
    return tour ? tour.show : true;
  }

  public skipTour(id: string): void {
    const tour = this.findOrCreateTour(id);
    tour.skipped = true;
    this.saveTours();
  }

  public completeTour(id: string): void {
    const tour = this.findOrCreateTour(id);
    tour.completed = true;
    this.saveTours();
  }

  private findTour(id: string): TourStatus | undefined {
    return this.tours.find((t) => t.id === id);
  }

  private findOrCreateTour(id: string): TourStatus {
    let tour = this.findTour(id);
    if (!tour) {
      tour = new TourStatus();
      tour.id = id;
    }
    this.tours.push(tour);
    return tour;
  }

  private saveTours(): void {
    const s = JSON.stringify(this);
    localStorage.setItem(tourKey, s);
  }
}
