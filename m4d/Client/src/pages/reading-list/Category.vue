<template>
  <div>
    <h3 class="category-description">{{ category.description }}</h3>
    <div v-for="book in category.books" :key="book.title">
      <book-component :book="book"></book-component>
    </div>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import BookComponent from "./Book.vue";
import { BookCategory } from "./BookCategory";

@Component({ components: { BookComponent } })
export default class Category extends Vue {
  @Prop() category!: BookCategory;
}
</script>

<style lang="scss" scoped>
.category-description {
  margin-top: 1em;
  font-size: 1.25em;
  font-weight: lighter;
}
</style>
