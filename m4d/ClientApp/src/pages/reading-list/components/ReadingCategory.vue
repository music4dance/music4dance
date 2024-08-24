<script setup lang="ts">
import { BookCategory } from "../models/BookCategory";

defineProps<{
  category: BookCategory;
  hideImages?: boolean;
}>();
</script>

<template>
  <div>
    <h3 class="category-description">{{ category.description }}</h3>
    <div v-for="book in category.books" :key="book.title">
      <BookComponent :book="book" :hide-images="hideImages"></BookComponent>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.category-description {
  margin-top: 1em;
  font-size: 1.25em;
  font-weight: lighter;
}
</style>
