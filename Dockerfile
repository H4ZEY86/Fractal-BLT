# Stage 1: Build the NativeAOT binary
FROM mcr.microsoft.com/dotnet/sdk:10.0-jammy AS build

# Install prerequisites for NativeAOT compilation
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /source

# Copy csproj and restore as distinct layers
COPY *.sln .
COPY FractalBltEncoder/FractalBltEncoder.csproj FractalBltEncoder/
COPY FractalGnnRouter/FractalGnnRouter.csproj FractalGnnRouter/
COPY FractalBridge/FractalBridge.csproj FractalBridge/
COPY FractalStreamer/FractalStreamer.csproj FractalStreamer/
COPY FractalCore/FractalCore.csproj FractalCore/
COPY FractalServe/FractalServe.csproj FractalServe/
COPY FractalStreamer.Tests/FractalStreamer.Tests.csproj FractalStreamer.Tests/
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Publish the API Server ahead-of-time (AOT) for Linux x64
RUN dotnet publish FractalServe/FractalServe.csproj -c Release -r linux-x64 /p:PublishAot=true -o /app

# Stage 2: Minimal Runtime Image
# We use NVIDIA's base image to ensure the CUDA Driver (libcuda.so.1) is present
FROM nvidia/cuda:12.2.0-base-ubuntu22.04

# Expose Kestrel Port
EXPOSE 5000

# We need the safetensors mounted here
VOLUME /models
ENV FRACTAL_MODEL="/models/model.safetensors"
ENV ASPNETCORE_URLS="http://0.0.0.0:5000"

WORKDIR /app

# Copy the compiled self-contained native binary
COPY --from=build /app/FractalServe .

# Execute the binary directly (no dotnet runtime needed)
ENTRYPOINT ["./FractalServe"]
