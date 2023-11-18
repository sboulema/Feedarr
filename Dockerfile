FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS sdk
WORKDIR /app
COPY . .
RUN dotnet publish --runtime linux-musl-x64 --self-contained -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
WORKDIR /app
COPY --from=sdk /app/out .
ENTRYPOINT ["./Feedarr"]