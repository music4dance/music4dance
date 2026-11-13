import { describe, expect, test } from "vitest";
import { mount } from "@vue/test-utils";
import MarkdownView from "@/components/MarkdownView.vue";

const render = (markdown: string) => mount(MarkdownView, { props: { markdown } });

describe("MarkdownView.vue", () => {
  test("renders emphasis", () => {
    const wrapper = render("This is *light* and **strong**.");
    expect(wrapper.find("em").text()).toBe("light");
    expect(wrapper.find("strong").text()).toBe("strong");
  });

  test("renders inline links", () => {
    const wrapper = render("See the [Slow Waltz](/dances/slow-waltz) page.");
    const link = wrapper.find("a");
    expect(link.text()).toBe("Slow Waltz");
    expect(link.attributes("href")).toBe("/dances/slow-waltz");
  });

  test("renders a hard line break from two trailing spaces", () => {
    const wrapper = render("First line  \nSecond line");
    expect(wrapper.findAll("p")).toHaveLength(1);
    expect(wrapper.find("p br").exists()).toBe(true);
  });

  test("separates paragraphs", () => {
    const wrapper = render("One.\n\nTwo.");
    expect(wrapper.findAll("p").map((p) => p.text())).toEqual(["One.", "Two."]);
  });

  test("strips script tags", () => {
    const wrapper = render('Hello <script>alert("x")</script> world');
    expect(wrapper.find("script").exists()).toBe(false);
    expect(wrapper.html()).not.toContain("alert");
  });

  test("neutralizes javascript: links", () => {
    const wrapper = render("[x](javascript:alert(1))");
    const href = wrapper.find("a").attributes("href");
    expect(href ?? "").not.toMatch(/^javascript:/i);
  });

  test("strips event handler attributes", () => {
    const wrapper = render('<img src="x" onerror="alert(1)">');
    expect(wrapper.html()).not.toContain("onerror");
  });

  test("updates when the markdown prop changes", async () => {
    const wrapper = render("*before*");
    await wrapper.setProps({ markdown: "**after**" });
    expect(wrapper.find("em").exists()).toBe(false);
    expect(wrapper.find("strong").text()).toBe("after");
  });
});
