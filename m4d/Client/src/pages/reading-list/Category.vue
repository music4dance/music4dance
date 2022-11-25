<template>
  <div>
    <h3 class="category-description">{{ category.description }}</h3>
    <div v-for="book in category.books" :key="book.title">
      <book-component :book="book"></book-component>
    </div>
  </div>
</template>

<script lang="ts">
import Vue, { PropType } from "vue";
import BookComponent from "./Book.vue";
import { BookCategory } from "./BookCategory";

export default Vue.extend({
  components: {
    BookComponent,
  },
  props: {
    category: {
      type: Object as PropType<BookCategory>,
      required: true,
    },
  },
});
</script>

<style lang="scss" scoped>
.category-description {
  margin-top: 1em;
  font-size: 1.25em;
  font-weight: lighter;
}
</style>
