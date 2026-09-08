# Stage 1: Build the NativeAOT binary
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build

# Install NativeAOT prerequisites for Ubuntu (noble)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy everything and publish
COPY . .
RUN dotnet publish FractalServe/FractalServe.csproj -c Release -r linux-x64 /p:PublishAot=true -o /app/publish

# Stage 2: Minimal runtime image
FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:10.0-noble-chiseled

WORKDIR /app
COPY --from=build /app/publish .

# Expose Kestrel port
EXPOSE 5000

# Set environment variable to ensure kestrel listens on all interfaces inside the container
ENV ASPNETCORE_URLS=http://+:5000

# Run the compiled NativeAOT binary
ENTRYPOINT ["./FractalServe"]
