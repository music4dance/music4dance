import Vue from "vue";

export default Vue.extend({
  data() {
    return new (class {
      selected: string[] = [];
    })();
  },
  methods: {
    selectSong(songId: string, selected: boolean): void {
      if (selected) {
        if (!this.selected.find((s) => s === songId)) {
          this.selected.push(songId);
        }
      } else {
        this.selected = this.selected.filter((s) => s !== songId);
      }
    },
  },
});
