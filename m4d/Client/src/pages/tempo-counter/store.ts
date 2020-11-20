import Vue from "vue";
import Vuex, { Store, GetterTree, MutationTree } from "vuex";

Vue.use(Vuex);

class State {
  public beatsPerMeasure = 4;
  public beatsPerMinute = 0;
  public countMethod = "measures";
  public espilonPercent = 5;

  constructor() {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const beatsPerMeasure = (window as any).initialNumerator;
    if (beatsPerMeasure) {
      this.beatsPerMeasure = beatsPerMeasure;
    }

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const beatsPerMinute = (window as any).initialTempo;
    if (beatsPerMinute) {
      this.beatsPerMinute = beatsPerMinute;
    }
  }
}

const getters: GetterTree<State, State> = {
  beatsPerMeasure(state: State): number {
    return state.beatsPerMeasure;
  },
  beatsPerMinute(state: State): number {
    return state.beatsPerMinute;
  },
  measuresPerMinute(state: State): number {
    return state.beatsPerMinute / state.beatsPerMeasure;
  },
  countMethod(state: State): string {
    return state.countMethod;
  },
  epsilonPercent(state: State): number {
    return state.espilonPercent;
  },
};

const mutations: MutationTree<State> = {
  updateBeatsPerMeasure(state: State, newBeatsPerMeasure: number) {
    state.beatsPerMeasure = newBeatsPerMeasure;
  },
  updateBeatsPerMinute(state: State, newBeatsPerMinute: number) {
    state.beatsPerMinute = newBeatsPerMinute;
  },
  updateMeasuresPerMinute(state: State, newMeasuresPerMinute: number) {
    state.beatsPerMinute = newMeasuresPerMinute * state.beatsPerMeasure;
  },
  updateCountMethod(state: State, newCountMethod: string) {
    state.countMethod = newCountMethod;
  },
  updateEpsilonPercent(state: State, newEpsilon: number) {
    state.espilonPercent = newEpsilon;
  },
};

export default new Store<State>({
  state: new State(),
  mutations,
  getters,
});
