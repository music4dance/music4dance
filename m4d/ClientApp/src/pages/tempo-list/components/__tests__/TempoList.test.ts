import { describe, test, expect } from "vitest";
import { mount } from "@vue/test-utils";
import TempoList from "../TempoList.vue";
import { DanceType } from "@/models/DanceDatabase/DanceType";
import { DanceInstance } from "@/models/DanceDatabase/DanceInstance";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import { TempoRange } from "@/models/DanceDatabase/TempoRange";
import { Meter } from "@/models/DanceDatabase/Meter";

const waltzGroup = new DanceGroup({ internalId: "WAL", internalName: "Waltz", danceIds: [] });
const countryGroup = new DanceGroup({ internalId: "CTY", internalName: "Country", danceIds: [] });

function buildDance(): DanceType {
  const dance = new DanceType({
    internalId: "SWZ",
    internalName: "Slow Waltz",
    meter: new Meter(3, 4),
    instances: [
      new DanceInstance({
        style: "International Standard",
        tempoRange: new TempoRange(84, 90),
        organizations: [],
      }),
      new DanceInstance({
        style: "Country",
        tempoRange: new TempoRange(80, 88),
        organizations: [],
      }),
    ],
  });
  // groups is a plain field populated by DanceDatabase.onDeserialized() in production; set it
  // directly here since we're building the fixture by hand.
  dance.groups = [waltzGroup, countryGroup];
  return dance;
}

function buildOtherDance(): DanceType {
  return new DanceType({
    internalId: "CHA",
    internalName: "Cha Cha",
    meter: new Meter(4, 4),
    instances: [
      new DanceInstance({
        style: "American Rhythm",
        tempoRange: new TempoRange(120, 128),
        organizations: [],
      }),
    ],
  });
}

function buildSalsa(): DanceType {
  return new DanceType({
    internalId: "SLS",
    internalName: "Salsa",
    meter: new Meter(4, 4),
    blogTag: "salsa",
    instances: [
      new DanceInstance({
        style: "American Rhythm",
        tempoRange: new TempoRange(200, 200),
        organizations: [],
      }),
      new DanceInstance({
        style: "Social",
        tempoRange: new TempoRange(160, 220),
        organizations: [],
        validation: { doubleTempoIfBelow: 120, halveTempoIfAbove: 250 },
      }),
    ],
  });
}

describe("TempoList.vue", () => {
  test("renders one row per dance, sorted by name ascending by default", () => {
    const wrapper = mount(TempoList, {
      props: { dances: [buildDance(), buildOtherDance()] },
    });

    const rows = wrapper.findAll("tbody tr");
    expect(rows.length).toBe(2);
    // "Cha Cha" sorts before "Slow Waltz"
    expect(rows[0]!.text()).toContain("Cha Cha");
    expect(rows[1]!.text()).toContain("Slow Waltz");
  });

  test("name cell links to the dance page", () => {
    const wrapper = mount(TempoList, { props: { dances: [buildDance()] } });

    const link = wrapper.find("a[href='/dances/slow-waltz']");
    expect(link.exists()).toBe(true);
    expect(link.text()).toBe("Slow Waltz");
  });

  test("meter, BPM, and MPM columns are formatted from the dance's tempo range", () => {
    const wrapper = mount(TempoList, { props: { dances: [buildDance()] } });
    const row = wrapper.find("tbody tr");

    expect(row.text()).toContain("3/4");
    // tempoRange spans both instances: 80-90
    expect(row.text()).toContain("80-90");
    // mpm = bpm / meter.numerator (3) = 26.7-30
    expect(row.text()).toContain(new TempoRange(80, 90).mpm(3));
  });

  test("BPM and MPM cells link to the tempo-filtered search", () => {
    const wrapper = mount(TempoList, { props: { dances: [buildDance()] } });

    const links = wrapper
      .findAll("a")
      .filter((a) => a.attributes("href")?.includes("advancedsearch"));
    expect(links.length).toBe(2);
    expect(links[0]!.attributes("href")).toBe(
      "/song/advancedsearch?dances=SWZ&tempomin=80&tempomax=90&sortorder=Dances",
    );
  });

  test("type cell links using only the dance's first group", () => {
    const wrapper = mount(TempoList, { props: { dances: [buildDance()] } });
    const row = wrapper.find("tbody tr");

    expect(row.text()).toContain("Waltz, Country");
    const groupLink = wrapper.find("a[href='/dances/Waltz']");
    expect(groupLink.exists()).toBe(true);
    expect(wrapper.find("a[href='/dances/Country']").exists()).toBe(false);
  });

  test("multi-word styles link, single-word styles render as plain text", () => {
    const wrapper = mount(TempoList, { props: { dances: [buildDance()] } });
    const row = wrapper.find("tbody tr");

    expect(row.text()).toContain("International Standard, Country");
    expect(wrapper.find("a[href='/dances/international-standard']").exists()).toBe(true);
    expect(wrapper.find("a[href='/dances/country']").exists()).toBe(false);
  });

  test("shows the empty-selection caption and no rows when there are no dances", () => {
    const wrapper = mount(TempoList, { props: { dances: [] } });

    expect(wrapper.text()).toContain("Please select at least one item from every drop-down");
    expect(wrapper.findAll("tbody tr").length).toBe(0);
  });

  describe("column chooser", () => {
    function mountList() {
      return mount(TempoList, { props: { dances: [buildDance()] } });
    }

    function columnCheckbox(wrapper: ReturnType<typeof mountList>, label: string) {
      const match = wrapper
        .find("#column-group")
        .findAll(".form-check")
        .find((fc) => fc.text() === label);
      if (!match) {
        throw new Error(`No checkbox labeled "${label}" found in #column-group`);
      }
      return match.find("input");
    }

    test("every optional column is visible by default", () => {
      const wrapper = mountList();
      const headerText = wrapper.find("thead").text();

      expect(headerText).toContain("Meter");
      expect(headerText).toContain("BPM");
      expect(headerText).toContain("MPM");
      expect(headerText).toContain("Type");
      expect(headerText).toContain("Styles");
    });

    test("the Name column is always shown and isn't offered as a choice", () => {
      const wrapper = mountList();
      const labels = wrapper
        .find("#column-group")
        .findAll(".form-check")
        .map((fc) => fc.text());

      expect(labels).toEqual(["Meter", "BPM", "MPM", "Type", "Styles", "Range"]);
    });

    test("unchecking a column in the chooser removes it from the table", async () => {
      const wrapper = mountList();

      await columnCheckbox(wrapper, "BPM").setValue(false);

      expect(wrapper.find("thead").text()).not.toContain("BPM");
      // 80-90 is the BPM cell's content; MPM (a different formatted value) still shows.
      expect(wrapper.find("tbody tr").text()).not.toContain("80-90");
      const link = wrapper.find("a[href='/dances/slow-waltz']");
      expect(link.exists()).toBe(true);
    });

    test("the Range column is hidden by default", () => {
      const wrapper = mountList();

      expect(wrapper.find("thead").text()).not.toContain("Range");
    });

    test("checking the Range column shows the validation-derived range, blank for dances without one", async () => {
      const wrapper = mount(TempoList, { props: { dances: [buildDance(), buildSalsa()] } });

      await columnCheckbox(wrapper, "Range").setValue(true);

      expect(wrapper.find("thead").text()).toContain("Range");
      const rows = wrapper.findAll("tbody tr");
      const salsaRow = rows.find((r) => r.text().includes("Salsa"))!;
      const waltzRow = rows.find((r) => r.text().includes("Slow Waltz"))!;
      expect(salsaRow.text()).toContain("120-250");
      // Slow Waltz has no validation data on either instance, so its Range cell is blank.
      expect(waltzRow.findAll("td").at(-1)!.text()).toBe("");
    });

    test("a footnote explaining Range only appears once that column is visible", async () => {
      const wrapper = mountList();

      expect(wrapper.text()).not.toContain("broadest tempo range");

      await columnCheckbox(wrapper, "Range").setValue(true);

      expect(wrapper.text()).toContain("broadest tempo range");
    });
  });

  describe("initialColumns", () => {
    function columnLabels(wrapper: ReturnType<typeof mount>) {
      return wrapper
        .find("#column-group")
        .findAll(".form-check input")
        .filter((i) => (i.element as HTMLInputElement).checked)
        .map((i) => i.element.closest(".form-check")?.textContent);
    }

    test("seeds the visible columns from the prop instead of each column's own default", () => {
      const wrapper = mount(TempoList, {
        props: { dances: [buildDance()], initialColumns: ["mpm", "validationRange"] },
      });

      expect(columnLabels(wrapper)).toEqual(["MPM", "Range"]);
      expect(wrapper.find("thead").text()).not.toContain("BPM");
      expect(wrapper.find("thead").text()).toContain("Range");
    });

    test("unknown column keys are silently dropped", () => {
      const wrapper = mount(TempoList, {
        props: { dances: [buildDance()], initialColumns: ["mpm", "not-a-real-column"] },
      });

      expect(columnLabels(wrapper)).toEqual(["MPM"]);
    });
  });

  describe("blog link", () => {
    test("a dance with a blogTag gets a 'Blog Posts' icon link next to its name", () => {
      const wrapper = mount(TempoList, { props: { dances: [buildSalsa()] } });

      const link = wrapper.find("a[title='Blog Posts']");
      expect(link.exists()).toBe(true);
      expect(link.attributes("href")).toBe("https://music4dance.blog/tag/salsa");
    });

    test("a dance with no blogTag gets no blog link", () => {
      const wrapper = mount(TempoList, { props: { dances: [buildDance()] } });

      expect(wrapper.find("a[title='Blog Posts']").exists()).toBe(false);
    });
  });
});
