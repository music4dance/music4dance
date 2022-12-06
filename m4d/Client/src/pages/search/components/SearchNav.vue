<template>
  <b-nav
    ><search-nav-item id="song-results" title="Songs" :active="active">
    </search-nav-item
    ><search-nav-item id="page-results" title="Pages" :active="active">
    </search-nav-item
    ><search-nav-item id="blog-results" title="Blog Posts" :active="active">
    </search-nav-item
    ><search-nav-item id="help-results" title="Help Pages" :active="active">
    </search-nav-item
  ></b-nav>
</template>

<script lang="ts">
import { Vue } from "vue-property-decorator";
import SearchNavItem from "./SearchNavItem.vue";

export default Vue.extend({
  components: { SearchNavItem },
  props: { active: { type: String, required: true } },
});
</script>
