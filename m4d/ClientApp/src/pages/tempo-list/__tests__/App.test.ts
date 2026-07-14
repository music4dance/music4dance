import { describe, test, expect, beforeEach, vi } from "vitest";
import { nextTick } from "vue";
import type { VueWrapper } from "@vue/test-utils";
import App from "../App.vue";
import { loadTestPage } from "@/helpers/TestPageSnapshot";
import { mockResizObserver } from "@/helpers/TestHelpers";
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
const TIMED_DANCE_COUNT = 40;

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
  const match = checkboxGroup(wrapper, groupId).find((fc) => fc.text() === label);
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

  test("deselecting every style empties the results, not 'show everything'", () => {
    const wrapper = mountTempoList();
    wrapper.vm.styles = [];

    expect(wrapper.vm.dances).toEqual([]);
  });

  test("the meter filter ignores the server-provided model (known gap)", () => {
    // TempoListModel.Meters is populated server-side from the ?meters= query param, but
    // App.vue's meters ref is seeded from a hard-coded option list and never reads model.meters.
    const wrapper = mountTempoList({ meters: ["3/4"] });

    expect(wrapper.vm.meters.length).toBe(3);
    expect(wrapper.vm.dances.length).toBe(TIMED_DANCE_COUNT);
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

  test("empty selection renders the TempoList empty-state caption", async () => {
    const wrapper = mountTempoList();
    const selectAll = wrapper.find("#style-all");
    await selectAll.setValue(false);
    await nextTick();

    expect(wrapper.text()).toContain("Please select at least one item from every drop-down");
    expect(wrapper.findAll("tbody tr").length).toBe(0);
  });
});
