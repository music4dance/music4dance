import { describe, test, expect, beforeEach, vi } from "vitest";
import { nextTick } from "vue";
import type { VueWrapper } from "@vue/test-utils";
import App from "../App.vue";
import { loadTestPage } from "@/helpers/TestPageSnapshot";
import { mockResizObserver } from "@/helpers/TestHelpers";
import { loadTestDances } from "@/helpers/LoadTestDances";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { Meter } from "@/models/DanceDatabase/Meter";
import { MenuContext } from "@/models/MenuContext";

const mockContext = new MenuContext({
  userName: "dwgray",
  roles: [],
  xsrfToken: "TEST_XSRF",
});

vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => mockContext,
}));

// Dances shown with every filter checkbox selected: every dance except the 9 "Performance" group
// dances and Pattern, none of which have a real tempo (see the "tempo-based exclusion" test).
const TIMED_DANCE_COUNT = DanceDatabase.load(loadTestDances()).dances.filter(
  (d) => !d.tempoRange.isInfinite,
).length;
function mountTempoList(
  model: Record<string, string[]> = {},
): VueWrapper<InstanceType<typeof App>> {
  return loadTestPage(App, model) as unknown as VueWrapper<InstanceType<typeof App>>;
}

function danceNames(wrapper: ReturnType<typeof mountTempoList>): string[] {
  return wrapper.vm.dances.map((d: { name: string }) => d.name).sort();
}

function checkboxGroup(wrapper: ReturnType<typeof mountTempoList>, groupId: string) {
  return wrapper.find(`#${groupId}`).findAll(".form-check");
}

function findCheckbox(wrapper: ReturnType<typeof mountTempoList>, groupId: string, label: string) {
  // The four page-level dropdowns render a "(N)" result count after each option's text (see
  // App.vue's `counts` props), so match the label with any trailing " (N)" stripped rather than
  // requiring an exact match.
  const match = checkboxGroup(wrapper, groupId).find(
    (fc) => fc.text().replace(/\s*\(\d+\)$/, "") === label,
  );
  if (!match) {
    throw new Error(`No checkbox labeled "${label}" found in #${groupId}`);
  }
  return match.find("input");
}

describe("tempo-list App.vue", () => {
  beforeEach(() => {
    mockResizObserver();
  });

  test("defaults to every filter selected and shows every timed, non-Performance dance", () => {
    const wrapper = mountTempoList();

    expect(wrapper.vm.styles.length).toBe(wrapper.vm.styleOptions.length);
    expect(wrapper.vm.types.length).toBe(wrapper.vm.typeOptions.length);
    expect(wrapper.vm.organizations.length).toBe(wrapper.vm.organizationOptions.length);
    expect(wrapper.vm.meters.length).toBe(3);

    expect(wrapper.vm.dances.length).toBe(TIMED_DANCE_COUNT);
    expect(danceNames(wrapper)).toContain("Cha Cha");
    // Organization-less "Social" dances are included once every organization is selected.
    expect(danceNames(wrapper)).toContain("Cross-step Waltz");
    expect(danceNames(wrapper)).toContain("Lindy Hop");
  });

  test("real checkbox interaction: unchecking then re-checking 'select all' on the meter dropdown restores every dance", async () => {
    // The Meter dropdown is the only one of the four whose option values are objects (Meter
    // instances) rather than strings - unlike the other three filters' real-interaction coverage,
    // this exercises BFormCheckboxGroup's v-model round-trip for an object-valued checkbox group.
    const wrapper = mountTempoList();

    await wrapper.find("#meter-all").setValue(false);
    expect(wrapper.vm.dances).toEqual([]);

    await wrapper.find("#meter-all").setValue(true);
    expect(wrapper.vm.dances.length).toBe(TIMED_DANCE_COUNT);
  });

  test("Performance dances, and any other dance with no real tempo, are excluded", () => {
    // App.vue used to exclude dances by group name ("Performance"), which missed Pattern - a
    // "Social"-style, "Other"-group dance that (like every Performance dance) has no actual
    // tempo: its only instance carries the same {min:1, max:500} placeholder range used to mark
    // "no meaningful tempo" (TempoRange.isInfinite). Filtering by tempoRange.isInfinite instead
    // catches both.
    const wrapper = mountTempoList();

    const names = danceNames(wrapper);
    expect(names).not.toContain("Jazz");
    expect(names).not.toContain("Contemporary");
    expect(names).not.toContain("Pattern");
  });

  test("the Type dropdown no longer offers a dead 'Performance' option", () => {
    // DanceDatabase.filter() used to rebuild its `groups` list by scanning the *original*,
    // pre-filter dance list instead of the dances it had just filtered, so "Performance" lingered
    // as a Type checkbox even though no Performance dance could ever be selected. Now that
    // App.vue excludes those dances by tempo up front and DanceDatabase.filter() derives groups
    // from the surviving dances, the option is gone entirely.
    const wrapper = mountTempoList();

    expect(wrapper.vm.typeOptions.map((o: { text: string }) => o.text)).not.toContain(
      "Performance",
    );
  });

  test("organization-less dances are shown when every organization is selected", () => {
    // Cross-step Waltz has no organization affiliation in the content data.
    // DanceFilter.matchOrganizations can never match an empty organizations list against
    // anything, so App.vue now normalizes "every organization checked" to `undefined` (no
    // restriction) rather than passing the full explicit list through - see the "combo" comment
    // in App.vue's `dances` computed.
    const wrapper = mountTempoList();

    expect(wrapper.vm.organizations.length).toBe(wrapper.vm.organizationOptions.length);
    expect(danceNames(wrapper)).toContain("Cross-step Waltz");
  });

  test("a deliberate, narrow organization selection still excludes organization-less dances", () => {
    // The "select all -> undefined" normalization only applies when every option is checked.
    // Selecting one specific organization must still mean "only dances affiliated with it" -
    // an organization-less dance shouldn't appear just because the filter isn't empty.
    const wrapper = mountTempoList();
    wrapper.vm.organizations = ["ucwdc"];

    expect(danceNames(wrapper)).not.toContain("Cross-step Waltz");
    expect(danceNames(wrapper)).not.toContain("Lindy Hop");
  });

  test("filters by style", () => {
    const wrapper = mountTempoList();
    wrapper.vm.styles = ["international-standard"];

    expect(danceNames(wrapper)).toEqual([
      "Quickstep",
      "Slow Foxtrot",
      "Slow Waltz",
      "Tango (Ballroom)",
      "Viennese Waltz",
    ]);
  });

  test("filters by type (dance group)", () => {
    const wrapper = mountTempoList();
    wrapper.vm.types = ["waltz"];

    expect(danceNames(wrapper)).toEqual([
      "Cross-step Waltz",
      "Slow Waltz",
      "Tango Vals",
      "Viennese Waltz",
    ]);
  });

  test("a dance's reported Type narrows to the selected group(s), like Styles/BPM/MPM already do", () => {
    // Viennese Waltz belongs to both the Waltz and Country groups. Before the fix, its Type
    // column always showed every group it belongs to ("Waltz, Country") regardless of which
    // Type checkboxes were selected - unlike Styles/BPM/MPM, which already narrow correctly
    // because they're derived from the (filter-narrowed) instances list.
    const wrapper = mountTempoList();
    wrapper.vm.types = ["waltz"];

    const viennese = wrapper.vm.dances.find(
      (d: { name: string }) => d.name === "Viennese Waltz",
    ) as { groups: { name: string }[] };
    expect(viennese.groups.map((g) => g.name)).toEqual(["Waltz"]);
  });

  test("filters by meter", () => {
    const wrapper = mountTempoList();
    wrapper.vm.meters = [new Meter(3, 4)];

    expect(danceNames(wrapper)).toEqual([
      "Cross-step Waltz",
      "Slow Waltz",
      "Tango Vals",
      "Viennese Waltz",
    ]);
  });

  test("filters by organization", () => {
    const wrapper = mountTempoList();
    wrapper.vm.organizations = ["ucwdc"];

    expect(danceNames(wrapper)).toEqual([
      "Cha Cha",
      "Country Two Step",
      "East Coast Swing",
      "Night Club Two Step",
      "Polka",
      "Slow Waltz",
      "Triple Two",
      "West Coast Swing",
    ]);
  });

  test("combines style, type, and meter filters (AND across dimensions)", () => {
    const wrapper = mountTempoList();
    wrapper.vm.styles = ["american-rhythm"];
    wrapper.vm.types = ["latin"];
    wrapper.vm.meters = [new Meter(4, 4)];

    // Paso Doble and Samba are Latin/American Rhythm but 2/4 meter, so the meter
    // filter excludes them even though the style and type filters would include them.
    expect(danceNames(wrapper)).toEqual([
      "Bachata",
      "Bolero",
      "Cha Cha",
      "Mambo",
      "Merengue",
      "Rumba",
      "Salsa",
    ]);
  });

  test("filters by dance name", () => {
    const wrapper = mountTempoList();
    wrapper.vm.nameFilter = "waltz";

    expect(danceNames(wrapper)).toEqual(["Cross-step Waltz", "Slow Waltz", "Viennese Waltz"]);
  });

  test("name filter combines with the other filters (AND across dimensions)", () => {
    const wrapper = mountTempoList();
    wrapper.vm.types = ["waltz"];
    // The Waltz group alone also includes "Tango Vals" (see "filters by type" above), but its
    // name doesn't contain "waltz", so the name filter narrows it out.
    wrapper.vm.nameFilter = "waltz";

    expect(danceNames(wrapper)).toEqual(["Cross-step Waltz", "Slow Waltz", "Viennese Waltz"]);

    wrapper.vm.nameFilter = "cross";
    expect(danceNames(wrapper)).toEqual(["Cross-step Waltz"]);
  });

  test("deselecting every style empties the results, not 'show everything'", () => {
    const wrapper = mountTempoList();
    wrapper.vm.styles = [];

    expect(wrapper.vm.dances).toEqual([]);
  });

  test("seeds meter selection from the server-provided model", () => {
    // TempoListModel.Meters is populated server-side from the ?meters= query param; App.vue's
    // meters ref now reads model.meters the same way styles/types/organizations already do.
    const wrapper = mountTempoList({ meters: ["3/4"] });

    expect(wrapper.vm.meters).toEqual([new Meter(3, 4)]);
    expect(danceNames(wrapper)).toEqual([
      "Cross-step Waltz",
      "Slow Waltz",
      "Tango Vals",
      "Viennese Waltz",
    ]);
  });

  test("an invalid server-provided meter is dropped rather than applied", () => {
    const wrapper = mountTempoList({ meters: ["5/4"] });

    // filterValidMeters() drops values that don't match a known option, leaving nothing selected.
    expect(wrapper.vm.meters).toEqual([]);
    expect(wrapper.vm.dances).toEqual([]);
  });

  test("seeds style selection from the server-provided model", () => {
    const wrapper = mountTempoList({ styles: ["international-standard"] });

    expect(wrapper.vm.styles).toEqual(["international-standard"]);
    expect(danceNames(wrapper)).toEqual([
      "Quickstep",
      "Slow Foxtrot",
      "Slow Waltz",
      "Tango (Ballroom)",
      "Viennese Waltz",
    ]);
  });

  test("an invalid server-provided style is dropped rather than applied", () => {
    const wrapper = mountTempoList({ styles: ["not-a-real-style"] });

    // filterValid() drops values that don't match a known option, leaving nothing selected.
    expect(wrapper.vm.styles).toEqual([]);
    expect(wrapper.vm.dances).toEqual([]);
  });

  test("seeds the visible TempoList columns from the server-provided ?columns= model", () => {
    // TempoListModel.Columns is populated server-side from the ?columns= query param, letting a
    // custom column set (e.g. the normally-hidden Range column) be linked to directly.
    const wrapper = mountTempoList({ columns: ["mpm", "validationRange"] });

    const headerText = wrapper.find("thead").text();
    expect(headerText).toContain("MPM");
    expect(headerText).toContain("Range");
    expect(headerText).not.toContain("BPM");
  });

  test("real checkbox interaction: unchecking 'select all' then checking 'Waltz' filters the table", async () => {
    const wrapper = mountTempoList();

    await wrapper.find("#type-all").setValue(false);
    await findCheckbox(wrapper, "type-group", "Waltz").setValue(true);
    await nextTick();

    const rows = wrapper.findAll("tbody tr");
    const rowText = rows.map((r) => r.text()).join(" | ");

    expect(rows.length).toBe(4);
    expect(rowText).toContain("Cross-step Waltz");
    expect(rowText).toContain("Slow Waltz");
    expect(rowText).toContain("Tango Vals");
    expect(rowText).toContain("Viennese Waltz");
    expect(rowText).not.toContain("Cha Cha");

    // TempoList's fields are [name, meter, bpm, mpm, groupName (Type), styles] in that order.
    const viennesRow = rows.find((r) => r.text().includes("Viennese Waltz"))!;
    const typeCell = viennesRow.findAll("td")[4]!;
    expect(typeCell.text()).toBe("Waltz");
  });

  test("per-option counts reflect the other filters' current selection, independent of this facet's own selection", () => {
    const wrapper = mountTempoList();
    wrapper.vm.types = ["waltz"];

    const styleOptions = wrapper.vm.styleOptions as { text: string; value: string }[];
    const styleCounts = wrapper.vm.styleCounts as number[];
    const indexOf = (value: string) => styleOptions.findIndex((o) => o.value === value);

    // With Type narrowed to Waltz: International Standard has Waltz-group dances (Slow Waltz,
    // Viennese Waltz), but American Rhythm (all Latin/Rhythm dances) has none - even though
    // "american-rhythm" isn't itself checked, proving the count for each style option is computed
    // independently of what's currently checked within the Style dropdown.
    expect(styleCounts[indexOf("international-standard")]).toBeGreaterThan(0);
    expect(styleCounts[indexOf("american-rhythm")]).toBe(0);
  });

  test("the default (everything selected) view gives every option a nonzero count", () => {
    // Every option is derived from the (already tempo-filtered) dance set, so with no other
    // restriction, every option must have at least one match.
    const wrapper = mountTempoList();

    expect(wrapper.vm.styleCounts as number[]).toSatisfy((c: number[]) => c.every((n) => n > 0));
    expect(wrapper.vm.typeCounts as number[]).toSatisfy((c: number[]) => c.every((n) => n > 0));
    expect(wrapper.vm.meterCounts as number[]).toSatisfy((c: number[]) => c.every((n) => n > 0));
    expect(wrapper.vm.organizationCounts as number[]).toSatisfy((c: number[]) =>
      c.every((n) => n > 0),
    );
  });

  test("organization counts use the strict single-organization count, not the 'select all' normalization", () => {
    // App.vue normalizes "every organization checked" to `undefined` for the *displayed* dances
    // (so organization-less dances like Cross-step Waltz show up by default), but each
    // organization's own count should still answer "how many dances are affiliated with just
    // this one" - matching the "filters by organization" test's 8-dance UCWDC result exactly.
    const wrapper = mountTempoList();

    const organizationOptions = wrapper.vm.organizationOptions as { text: string; value: string }[];
    const organizationCounts = wrapper.vm.organizationCounts as number[];
    const ucwdcIndex = organizationOptions.findIndex((o) => o.value === "ucwdc");

    expect(organizationCounts[ucwdcIndex]).toBe(8);
  });

  test("real DOM: the Style dropdown grays out a zero-count option once Type narrows to Waltz", async () => {
    const wrapper = mountTempoList();
    wrapper.vm.types = ["waltz"];
    await nextTick();

    const americanRhythmRow = checkboxGroup(wrapper, "style-group").find((fc) =>
      fc.text().startsWith("American Rhythm"),
    )!;
    expect(americanRhythmRow.text()).toBe("American Rhythm (0)");
    expect(americanRhythmRow.find(".form-check-label > span").classes("text-muted")).toBe(true);

    const internationalStandardRow = checkboxGroup(wrapper, "style-group").find((fc) =>
      fc.text().startsWith("International Standard"),
    )!;
    expect(internationalStandardRow.find(".form-check-label > span").classes("text-muted")).toBe(
      false,
    );
  });

  test("empty selection renders the TempoList empty-state caption", async () => {
    const wrapper = mountTempoList();
    const selectAll = wrapper.find("#style-all");
    await selectAll.setValue(false);
    await nextTick();

    expect(wrapper.text()).toContain("Please select at least one item from every drop-down");
    expect(wrapper.findAll("tbody tr").length).toBe(0);
  });
});
