# Frontend

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.21.

Talks to the ProductivityHub .NET API over relative `/api/...` paths (proxied
in dev, same-origin in prod behind Nginx — see `nginx/`) and to
`realtime-service` via Socket.IO for the chat feature. No CORS setup is
needed on either side because of this.

## Development server

`proxy.conf.json` forwards `/api/*` to `http://localhost:8080` (the
containerized `api` service's port). If you're instead running the API
natively (`dotnet run` from `API-server/`, port `5043`), edit
`proxy.conf.json`'s target accordingly before running `ng serve`. The
Socket.IO client connects directly to `http://localhost:4000` in dev
(`environment.development.ts`) — `realtime-service` must be running there.

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
