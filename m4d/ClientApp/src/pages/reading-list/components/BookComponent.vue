<script setup lang="ts">
import { computed } from "vue";
import { type Book, type Link } from "../models/BookModel";

const props = defineProps<{
  book: Book;
}>();

const links = computed(() => {
  const links: Link[] = [];
  const book = props.book;
  if (book.kindle) {
    links.push({ text: `${book.title} (Kindle)`, ref: book.kindle });
  }
  if (book.paperback) {
    links.push({ text: `${book.title} (Paperback)`, ref: book.paperback });
  }
  if (book.hardcover) {
    links.push({ text: `${book.title} (Hardcover)`, ref: book.hardcover });
  }
  if (book.audible) {
    links.push({ text: `${book.title} (Audible)`, ref: book.audible });
  }
  if (book.review) {
    links.push({ text: "Review", ref: book.review });
  }

  return book.others ? [...links, ...book.others] : links;
});
</script>

<template>
  <div>
    <hr />
    <h2 class="title">
      {{ book.title }}<span v-if="book.subtitle"> : {{ book.subtitle }}</span>
    </h2>
    <div>by {{ book.author }}</div>
    <div style="display: flex" class="mt-2">
      <div v-html="book.image"></div>
      <div>
        <ul style="list-style-type: none">
          <li v-for="link in links" :key="link.ref">
            <a :href="link.ref" target="_blank">{{ link.text }}</a>
          </li>
        </ul>
        <div class="notes">{{ book.notes }}</div>
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.title {
  margin-top: 1rem;
  margin-bottom: 0;
  font-size: 1.25rem;
}
.notes {
  margin-left: 2rem;
}
</style>
