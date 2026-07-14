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

// Dances shown with every filter checkbox selected. This is NOT all 41 non-Performance dances:
// DanceFilter.matchOrganizations uses `type.organizations.some(...)`, which is false for any
// dance whose instances list no organizations at all (e.g. "Social"-only dances like Cross-step
// Waltz or Lindy Hop) regardless of which organizations are selected. So "select all" silently
// hides every organization-less dance — see the dedicated test below and the architecture doc's
// "Known Gaps" section.
const DEFAULT_FILTERED_COUNT = 23;

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

  test("defaults to every filter selected and shows every non-Performance dance", () => {
    const wrapper = mountTempoList();

    expect(wrapper.vm.styles.length).toBe(wrapper.vm.styleOptions.length);
    expect(wrapper.vm.types.length).toBe(wrapper.vm.typeOptions.length);
    expect(wrapper.vm.organizations.length).toBe(wrapper.vm.organizationOptions.length);
    expect(wrapper.vm.meters.length).toBe(3);

    expect(wrapper.vm.dances.length).toBe(DEFAULT_FILTERED_COUNT);
    expect(danceNames(wrapper)).toContain("Cha Cha");
    expect(danceNames(wrapper)).not.toContain("Jazz");
  });

  test("the Type dropdown still lists 'Performance' even though no Performance dance can ever show", () => {
    // DanceDatabase.filter() rebuilds its `groups` list by scanning `this.dances` — the
    // *original*, pre-filter dance list — instead of the `dances` it just filtered
    // (DanceDatabase.ts:119). So even though App.vue filters "Performance" out of
    // danceDatabase.dances up front, danceDatabase.groups (and therefore the Type dropdown)
    // still contains a "Performance" checkbox. Selecting only it yields zero results, because
    // there are no Performance dances left to match.
    const wrapper = mountTempoList();

    expect(wrapper.vm.typeOptions.map((o: { text: string }) => o.text)).toContain("Performance");

    wrapper.vm.types = ["performance"];
    expect(wrapper.vm.dances).toEqual([]);
  });

  test("organization-less dances are hidden even with every organization selected", () => {
    // Cross-step Waltz and Lindy Hop are "Social"-style dances with no organization affiliation
    // in the content data. Because DanceFilter.matchOrganizations checks
    // `type.organizations.some(...)`, an empty organizations array can never match, even though
    // every organization checkbox is checked. This is the reason DEFAULT_FILTERED_COUNT (23) is
    // well under the 41 non-Performance dances that exist.
    const wrapper = mountTempoList();

    expect(wrapper.vm.organizations.length).toBe(wrapper.vm.organizationOptions.length);
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

    // Cross-step Waltz and Tango Vals are also in the Waltz group but have no organization
    // affiliation, so the (default, "all selected") organizations filter excludes them — see
    // "organization-less dances are hidden" above.
    expect(danceNames(wrapper)).toEqual(["Slow Waltz", "Viennese Waltz"]);
  });

  test("filters by meter", () => {
    const wrapper = mountTempoList();
    wrapper.vm.meters = [new Meter(3, 4)];

    expect(danceNames(wrapper)).toEqual(["Slow Waltz", "Viennese Waltz"]);
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
    expect(wrapper.vm.dances.length).toBe(DEFAULT_FILTERED_COUNT);
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

    expect(rows.length).toBe(2);
    expect(rowText).toContain("Slow Waltz");
    expect(rowText).toContain("Viennese Waltz");
    expect(rowText).not.toContain("Cha Cha");
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
