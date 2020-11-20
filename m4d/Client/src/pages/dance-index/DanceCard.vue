<template>
  <b-card
    no-body
    border-variant="primary"
    style="margin-bottom: 1rem; min-width: 15rem"
  >
    <b-card-header header-bg-variant="primary" header-text-variant="white">
      <dance-item
        :dance="group"
        variant="secondary"
        text-style="color:white"
      ></dance-item>
    </b-card-header>

    <dance-list :group="group" :flush="true"></dance-list>
  </b-card>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import { DanceStats } from "@/model/DanceStats";
import DanceItem from "@/components/DanceItem.vue";
import DanceList from "@/components/DanceList.vue";

@Component({
  components: {
    DanceItem,
    DanceList,
  },
})
export default class DanceCard extends Vue {
  @Prop() private group!: DanceStats;
}
</script>
