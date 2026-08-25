#define _GNU_SOURCE

#include <libheif/heif.h>

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <dlfcn.h>
#include <limits.h>
#include <unistd.h>
#endif

static void print_heif_error(const char* operation, struct heif_error error)
{
  fprintf(stderr,
          "%s failed: code=%d subcode=%d message=%s\n",
          operation,
          error.code,
          error.subcode,
          error.message == NULL ? "<none>" : error.message);
}

static int get_loaded_library_path(char* buffer, size_t buffer_size)
{
#ifdef _WIN32
  HMODULE module = NULL;
  if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                             GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                         (LPCSTR)(const void*)&heif_get_version,
                         &module)) {
    return 0;
  }

  DWORD length = GetModuleFileNameA(module, buffer, (DWORD)buffer_size);
  return length > 0 && length < buffer_size;
#else
  Dl_info info;
  if (dladdr((const void*)&heif_get_version, &info) == 0 || info.dli_fname == NULL) {
    return 0;
  }

  char resolved[PATH_MAX];
  const char* path = realpath(info.dli_fname, resolved);
  if (path == NULL) {
    path = info.dli_fname;
  }

  if (strlen(path) + 1 > buffer_size) {
    return 0;
  }

  strcpy(buffer, path);
  return 1;
#endif
}

static int decode_file(const char* label, const char* path)
{
  struct heif_context* context = heif_context_alloc();
  if (context == NULL) {
    fprintf(stderr, "%s context allocation failed\n", label);
    return 0;
  }

  struct heif_error error = heif_context_read_from_file(context, path, NULL);
  if (error.code != heif_error_Ok) {
    print_heif_error(label, error);
    heif_context_free(context);
    return 0;
  }

  struct heif_image_handle* handle = NULL;
  error = heif_context_get_primary_image_handle(context, &handle);
  if (error.code != heif_error_Ok || handle == NULL) {
    print_heif_error(label, error);
    heif_context_free(context);
    return 0;
  }

  int width = heif_image_handle_get_width(handle);
  int height = heif_image_handle_get_height(handle);
  int luma_bits = heif_image_handle_get_luma_bits_per_pixel(handle);
  int chroma_bits = heif_image_handle_get_chroma_bits_per_pixel(handle);

  struct heif_image* image = NULL;
  error = heif_decode_image(handle,
                            &image,
                            heif_colorspace_RGB,
                            heif_chroma_interleaved_RGBA,
                            NULL);
  if (error.code != heif_error_Ok || image == NULL) {
    print_heif_error(label, error);
    heif_image_handle_release(handle);
    heif_context_free(context);
    return 0;
  }

  int stride = 0;
  const uint8_t* pixels = heif_image_get_plane_readonly(
      image, heif_channel_interleaved, &stride);
  int valid = width > 0 && height > 0 && pixels != NULL && stride >= width * 4;

  printf("%s.width=%d\n", label, width);
  printf("%s.height=%d\n", label, height);
  printf("%s.lumaBits=%d\n", label, luma_bits);
  printf("%s.chromaBits=%d\n", label, chroma_bits);
  printf("%s.alpha=%d\n", label, heif_image_handle_has_alpha_channel(handle));
  printf("%s.decode=%s\n", label, valid ? "PASS" : "FAIL");

  heif_image_release(image);
  heif_image_handle_release(handle);
  heif_context_free(context);
  return valid;
}

int main(int argc, char** argv)
{
  if (argc != 3) {
    fprintf(stderr, "Usage: fovium-libheif-smoke <heif-file> <avif-file>\n");
    return 2;
  }

  struct heif_error init_error = heif_init(NULL);
  if (init_error.code != heif_error_Ok) {
    print_heif_error("heif_init", init_error);
    return 3;
  }

  char loaded_path[4096];
  if (!get_loaded_library_path(loaded_path, sizeof(loaded_path))) {
    fprintf(stderr, "Could not determine loaded libheif path\n");
    heif_deinit();
    return 4;
  }

  int hevc_decoder = heif_have_decoder_for_format(heif_compression_HEVC);
  int av1_decoder = heif_have_decoder_for_format(heif_compression_AV1);
  int hevc_encoder = heif_have_encoder_for_format(heif_compression_HEVC);
  int av1_encoder = heif_have_encoder_for_format(heif_compression_AV1);

  printf("runtime.path=%s\n", loaded_path);
  printf("runtime.version=%s\n", heif_get_version());
  printf("decoder.hevc=%d\n", hevc_decoder);
  printf("decoder.av1=%d\n", av1_decoder);
  printf("encoder.hevc=%d\n", hevc_encoder);
  printf("encoder.av1=%d\n", av1_encoder);

  int heif_ok = hevc_decoder && decode_file("heif", argv[1]);
  int avif_ok = av1_decoder && decode_file("avif", argv[2]);
  int decoder_only = !hevc_encoder && !av1_encoder;
  int success = heif_ok && avif_ok && decoder_only;

  printf("result=%s\n", success ? "PASS" : "FAIL");
  heif_deinit();
  return success ? 0 : 5;
}
