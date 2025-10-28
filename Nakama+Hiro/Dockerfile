ARG NAKAMA_VERSION=3.32.0

FROM docker.io/heroiclabs/nakama-pluginbuilder:${NAKAMA_VERSION} AS builder

ENV GO111MODULE=on
ENV CGO_ENABLED=1

WORKDIR /backend
COPY . .

RUN go build --trimpath --buildmode=plugin -o ./backend.so

FROM docker.io/heroiclabs/nakama:${NAKAMA_VERSION}

COPY --from=builder /backend/backend.so /nakama/data/modules
COPY --from=builder /backend/local.yml /nakama/data/
COPY --from=builder /backend/definitions/dev1/*.json /nakama/data/modules/definitions/dev1/
COPY --from=builder /backend/lib/*.bin /nakama/data/modules/lib/