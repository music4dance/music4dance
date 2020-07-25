<template>
  <page id="app">
    <counter />
    <dance-list />
  </page>
</template>

<script lang="ts">
import { Component, Vue } from 'vue-property-decorator';
import Page from '../components/Page.vue';
import Counter from './components/Counter.vue';
import DanceList from './components/DanceList.vue';

@Component({
  components: {
    Counter,
    DanceList,
    Page,
  },
})
export default class App extends Vue {}
</script>

<style lang="scss">
#app {
  font-family: 'Avenir', Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  text-align: center;
  color: #2c3e50;
}
</style>
