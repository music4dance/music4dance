# tempo-counter

## TodoNext

- General
  - Make tempo dance competition category table use a "short" name for dances
    - Probably have to modify DanceInstance to do this
  - Consider breadcrumbs for tools & search pages
  - Improve Breadcrumbs for accounts pages
  - Look at remaining simple asp.net mvc pages with old layout
    - Error pages, others?
- Tempo Counter
  - Refactor? Can we put stuff further down in the dance heirarchy
  - Make click action on dance configurable (use a slot?)
  - Manual tempo entry: Consider making MPM/BMP symetrical + enable mini-phrase/other phrases?
  - Should we enable all (or some of) the parameters we had in the old control

## Project setup

```
yarn install
```

### Compiles and hot-reloads for stand-alone development

```
yarn run serve
```

### Compiles and hot-reloads for dev in context of m4d

```
yarn run watch
```

### Compiles and minifies for production

```
yarn run build
```

### Run your tests

```
yarn run test
```

### Lints and fixes files

```
yarn run lint
```

### Run your unit tests

```
yarn run test:unit
```

### Customize configuration

See [Configuration Reference](https://cli.vuejs.org/config/).

### Turn off ts-lint log error

```
// tslint:disable-next-line:no-console
```
