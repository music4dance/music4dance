import Vue from 'vue';
import Vuex, { Store, ActionTree, ActionContext, GetterTree, MutationTree } from 'vuex';

Vue.use(Vuex);

class State {
  public beatsPerMeasure: number = 4;
  public beatsPerMinute: number = 0;
  public countMethod: string = 'measures';
  public espilonPercent: number = 5;

  constructor() {
    const beatsPerMeasure = (window as any).initialNumerator;
    if (beatsPerMeasure) {
      this.beatsPerMeasure = beatsPerMeasure;
    }

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
