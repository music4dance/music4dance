declare module "vue-mq" {
  import { PluginObject } from "vue";

  /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
  interface VueMq extends PluginObject<any> {
    VueMq: VueMq;
  }

  const VueMq: VueMq;
  export default VueMq;
}
