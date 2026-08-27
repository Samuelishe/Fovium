#if !defined(_WIN32)
#define _GNU_SOURCE
#endif

#include <lcms2.h>

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <dlfcn.h>
#include <limits.h>
#include <pthread.h>
#include <unistd.h>
#endif

#define EXPECTED_VERSION 2190
#define ICC_SIZE_LIMIT (16u * 1024u * 1024u)
#define THREAD_COUNT 4
#define THREAD_ITERATIONS 8

typedef struct ProfileBytes {
    unsigned char* data;
    cmsUInt32Number size;
} ProfileBytes;

typedef struct WorkerState {
    const ProfileBytes* profile;
    int passed;
} WorkerState;

typedef struct MalformedResults {
    int empty;
    int bad_signature;
    int truncated;
    int impossible_size;
    int oversized_admission;
} MalformedResults;

static void quiet_error_handler(cmsContext context, cmsUInt32Number code, const char* text)
{
    (void) context;
    (void) code;
    (void) text;
}

static int within_tolerance(unsigned char actual, unsigned char expected, unsigned char tolerance)
{
    int difference = (int) actual - (int) expected;
    if (difference < 0) {
        difference = -difference;
    }

    return difference <= tolerance;
}

static int profile_size_is_admissible(size_t size)
{
    return size > 0 && size <= ICC_SIZE_LIMIT;
}

static int get_runtime_path(char* destination, size_t capacity)
{
#if defined(_WIN32)
    HMODULE module = NULL;
    DWORD length;
    if (!GetModuleHandleExA(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            (LPCSTR) (uintptr_t) &cmsGetEncodedCMMversion,
            &module)) {
        return 0;
    }

    length = GetModuleFileNameA(module, destination, (DWORD) capacity);
    return length > 0 && length < capacity;
#else
    Dl_info info;
    char resolved[PATH_MAX];
    if (dladdr((void*) (uintptr_t) &cmsGetEncodedCMMversion, &info) == 0 || info.dli_fname == NULL) {
        return 0;
    }

    if (realpath(info.dli_fname, resolved) == NULL || strlen(resolved) + 1 > capacity) {
        return 0;
    }

    memcpy(destination, resolved, strlen(resolved) + 1);
    return 1;
#endif
}

static int runtime_is_local(const char* runtime_path, const char* expected_directory)
{
    char directory[4096];
    size_t directory_length;
#if defined(_WIN32)
    DWORD length = GetFullPathNameA(expected_directory, (DWORD) sizeof(directory), directory, NULL);
    if (length == 0 || length >= sizeof(directory)) {
        return 0;
    }
#else
    if (realpath(expected_directory, directory) == NULL) {
        return 0;
    }
#endif

    directory_length = strlen(directory);
    if (directory_length == 0 || directory_length + 1 >= strlen(runtime_path)) {
        return 0;
    }

#if defined(_WIN32)
    if (_strnicmp(runtime_path, directory, directory_length) != 0) {
        return 0;
    }
#else
    if (strncmp(runtime_path, directory, directory_length) != 0) {
        return 0;
    }
#endif

    return runtime_path[directory_length] == '/' || runtime_path[directory_length] == '\\';
}

static cmsHPROFILE create_linear_rgb_profile(cmsContext context)
{
    const cmsCIExyY white_point = { 0.3127, 0.3290, 1.0 };
    const cmsCIExyYTRIPLE primaries = {
        { 0.6400, 0.3300, 1.0 },
        { 0.3000, 0.6000, 1.0 },
        { 0.1500, 0.0600, 1.0 }
    };
    cmsToneCurve* curves[3] = { NULL, NULL, NULL };
    cmsHPROFILE profile = NULL;
    int index;

    for (index = 0; index < 3; index++) {
        curves[index] = cmsBuildGamma(context, 1.0);
        if (curves[index] == NULL) {
            goto cleanup;
        }
    }

    profile = cmsCreateRGBProfileTHR(context, &white_point, &primaries, curves);

cleanup:
    for (index = 0; index < 3; index++) {
        if (curves[index] != NULL) {
            cmsFreeToneCurve(curves[index]);
        }
    }

    return profile;
}

static int run_matrix_transform(cmsContext context)
{
    static const unsigned char input[][3] = {
        { 0, 0, 0 },
        { 32, 64, 128 },
        { 128, 192, 255 },
        { 255, 255, 255 }
    };
    static const unsigned char expected[][3] = {
        { 0, 0, 0 },
        { 4, 13, 55 },
        { 55, 134, 255 },
        { 255, 255, 255 }
    };
    unsigned char output[sizeof(input) / sizeof(input[0])][3] = { 0 };
    cmsHPROFILE source = NULL;
    cmsHPROFILE destination = NULL;
    cmsHTRANSFORM transform = NULL;
    size_t patch;
    size_t channel;
    int passed = 0;

    source = cmsCreate_sRGBProfileTHR(context);
    destination = create_linear_rgb_profile(context);
    if (source == NULL || destination == NULL || !cmsIsMatrixShaper(source) || !cmsIsMatrixShaper(destination)) {
        goto cleanup;
    }

    transform = cmsCreateTransformTHR(
        context,
        source,
        TYPE_RGB_8,
        destination,
        TYPE_RGB_8,
        INTENT_RELATIVE_COLORIMETRIC,
        0);
    if (transform == NULL) {
        goto cleanup;
    }

    cmsDoTransform(transform, input, output, (cmsUInt32Number) (sizeof(input) / sizeof(input[0])));
    for (patch = 0; patch < sizeof(input) / sizeof(input[0]); patch++) {
        for (channel = 0; channel < 3; channel++) {
            if (!within_tolerance(output[patch][channel], expected[patch][channel], 1)) {
                goto cleanup;
            }
        }
    }

    printf("matrix.patch0=%u,%u,%u\n", output[0][0], output[0][1], output[0][2]);
    printf("matrix.patch1=%u,%u,%u\n", output[1][0], output[1][1], output[1][2]);
    printf("matrix.patch2=%u,%u,%u\n", output[2][0], output[2][1], output[2][2]);
    printf("matrix.patch3=%u,%u,%u\n", output[3][0], output[3][1], output[3][2]);
    passed = 1;

cleanup:
    if (transform != NULL) {
        cmsDeleteTransform(transform);
    }
    if (destination != NULL) {
        cmsCloseProfile(destination);
    }
    if (source != NULL) {
        cmsCloseProfile(source);
    }
    return passed;
}

static int lut_sampler(const cmsUInt16Number input[], cmsUInt16Number output[], void* cargo)
{
    (void) cargo;
    output[0] = input[2];
    output[1] = (cmsUInt16Number) (65535u - input[1]);
    output[2] = input[0];
    return 1;
}

static int create_lut_profile(cmsContext context, ProfileBytes* result)
{
    cmsHPROFILE profile = NULL;
    cmsPipeline* pipeline = NULL;
    cmsStage* input_curves = NULL;
    cmsStage* clut = NULL;
    cmsStage* output_curves = NULL;
    cmsUInt32Number size = 0;
    unsigned char* data = NULL;
    const char* failure = "allocation";
    int passed = 0;

    profile = cmsCreateProfilePlaceholder(context);
    pipeline = cmsPipelineAlloc(context, 3, 3);
    input_curves = cmsStageAllocToneCurves(context, 3, NULL);
    clut = cmsStageAllocCLut16bit(context, 2, 3, 3, NULL);
    output_curves = cmsStageAllocToneCurves(context, 3, NULL);
    if (profile == NULL || pipeline == NULL || input_curves == NULL || clut == NULL || output_curves == NULL) {
        goto cleanup;
    }

    cmsSetProfileVersion(profile, 4.3);
    cmsSetDeviceClass(profile, cmsSigLinkClass);
    cmsSetColorSpace(profile, cmsSigRgbData);
    cmsSetPCS(profile, cmsSigRgbData);
    cmsSetHeaderRenderingIntent(profile, INTENT_RELATIVE_COLORIMETRIC);

    failure = "CLUT sampling or pipeline insertion";
    if (!cmsStageSampleCLut16bit(clut, lut_sampler, NULL, 0) ||
        !cmsPipelineInsertStage(pipeline, cmsAT_END, input_curves)) {
        goto cleanup;
    }
    input_curves = NULL;
    if (!cmsPipelineInsertStage(pipeline, cmsAT_END, clut)) {
        goto cleanup;
    }
    clut = NULL;
    if (!cmsPipelineInsertStage(pipeline, cmsAT_END, output_curves)) {
        goto cleanup;
    }
    output_curves = NULL;

    failure = "AToB0 write or profile sizing";
    if (!cmsWriteTag(profile, cmsSigAToB0Tag, pipeline) ||
        !cmsSaveProfileToMem(profile, NULL, &size) ||
        !profile_size_is_admissible(size)) {
        goto cleanup;
    }

    data = (unsigned char*) malloc(size);
    failure = "profile serialization";
    if (data == NULL || !cmsSaveProfileToMem(profile, data, &size)) {
        goto cleanup;
    }

    result->data = data;
    result->size = size;
    data = NULL;
    passed = 1;

cleanup:
    if (!passed) {
        fprintf(stderr, "LUT profile creation failed during %s\n", failure);
    }
    if (output_curves != NULL) {
        cmsStageFree(output_curves);
    }
    free(data);
    if (clut != NULL) {
        cmsStageFree(clut);
    }
    if (input_curves != NULL) {
        cmsStageFree(input_curves);
    }
    if (pipeline != NULL) {
        cmsPipelineFree(pipeline);
    }
    if (profile != NULL) {
        cmsCloseProfile(profile);
    }
    return passed;
}

static int transform_lut_bytes(cmsContext context, const ProfileBytes* bytes, int print_patches)
{
    static const unsigned char input[][3] = {
        { 0, 0, 0 },
        { 17, 63, 129 },
        { 128, 192, 240 },
        { 255, 255, 255 }
    };
    static const unsigned char expected[][3] = {
        { 0, 255, 0 },
        { 129, 192, 17 },
        { 240, 63, 128 },
        { 255, 0, 255 }
    };
    unsigned char output[sizeof(input) / sizeof(input[0])][3] = { 0 };
    cmsHPROFILE profile = NULL;
    cmsHTRANSFORM transform = NULL;
    size_t patch;
    size_t channel;
    int passed = 0;

    profile = cmsOpenProfileFromMemTHR(context, bytes->data, bytes->size);
    if (profile == NULL || !cmsIsCLUT(profile, INTENT_RELATIVE_COLORIMETRIC, LCMS_USED_AS_INPUT)) {
        goto cleanup;
    }

    transform = cmsCreateTransformTHR(
        context,
        profile,
        TYPE_RGB_8,
        NULL,
        TYPE_RGB_8,
        INTENT_RELATIVE_COLORIMETRIC,
        0);
    if (transform == NULL) {
        goto cleanup;
    }

    cmsDoTransform(transform, input, output, (cmsUInt32Number) (sizeof(input) / sizeof(input[0])));
    for (patch = 0; patch < sizeof(input) / sizeof(input[0]); patch++) {
        for (channel = 0; channel < 3; channel++) {
            if (output[patch][channel] != expected[patch][channel]) {
                goto cleanup;
            }
        }
    }

    if (print_patches) {
        printf("lut.patch0=%u,%u,%u\n", output[0][0], output[0][1], output[0][2]);
        printf("lut.patch1=%u,%u,%u\n", output[1][0], output[1][1], output[1][2]);
        printf("lut.patch2=%u,%u,%u\n", output[2][0], output[2][1], output[2][2]);
        printf("lut.patch3=%u,%u,%u\n", output[3][0], output[3][1], output[3][2]);
    }
    passed = 1;

cleanup:
    if (transform != NULL) {
        cmsDeleteTransform(transform);
    }
    if (profile != NULL) {
        cmsCloseProfile(profile);
    }
    return passed;
}

static int open_profile_fails(cmsContext context, const void* data, cmsUInt32Number size)
{
    cmsHPROFILE profile = cmsOpenProfileFromMemTHR(context, data, size);
    if (profile == NULL) {
        return 1;
    }
    cmsCloseProfile(profile);
    return 0;
}

static MalformedResults check_malformed_profiles(cmsContext context, const ProfileBytes* valid)
{
    unsigned char bad_magic[128] = { 0 };
    unsigned char impossible_size[128] = { 0 };
    unsigned char* bad_valid = NULL;
    MalformedResults results = { 0, 0, 0, 0, 0 };

    results.empty = open_profile_fails(context, bad_magic, 0);

    bad_magic[0] = 0;
    bad_magic[1] = 0;
    bad_magic[2] = 0;
    bad_magic[3] = 128;
    memcpy(bad_magic + 36, "nope", 4);
    results.bad_signature = open_profile_fails(context, bad_magic, sizeof(bad_magic));

    bad_valid = (unsigned char*) malloc(valid->size);
    if (bad_valid == NULL) {
        return results;
    }
    memcpy(bad_valid, valid->data, valid->size);
    memcpy(bad_valid + 36, "nope", 4);
    results.bad_signature = results.bad_signature && open_profile_fails(context, bad_valid, valid->size);
    results.truncated = open_profile_fails(context, valid->data, 64);

    impossible_size[0] = 0x7f;
    impossible_size[1] = 0xff;
    impossible_size[2] = 0xff;
    impossible_size[3] = 0xff;
    memcpy(impossible_size + 36, "acsp", 4);
    results.impossible_size = open_profile_fails(context, impossible_size, sizeof(impossible_size));
    results.oversized_admission = !profile_size_is_admissible((size_t) ICC_SIZE_LIMIT + 1u);
    free(bad_valid);
    return results;
}

static int run_worker(WorkerState* state)
{
    int iteration;
    state->passed = 0;
    for (iteration = 0; iteration < THREAD_ITERATIONS; iteration++) {
        cmsContext context = cmsCreateContext(NULL, NULL);
        int passed;
        if (context == NULL) {
            return 0;
        }
        cmsSetLogErrorHandlerTHR(context, quiet_error_handler);
        passed = transform_lut_bytes(context, state->profile, 0);
        cmsDeleteContext(context);
        if (!passed) {
            return 0;
        }
    }
    state->passed = 1;
    return 1;
}

#if defined(_WIN32)
static DWORD WINAPI worker_entry(LPVOID parameter)
{
    return run_worker((WorkerState*) parameter) ? 0u : 1u;
}
#else
static void* worker_entry(void* parameter)
{
    return (void*) (intptr_t) (run_worker((WorkerState*) parameter) ? 0 : 1);
}
#endif

static int run_concurrency(const ProfileBytes* profile)
{
    WorkerState states[THREAD_COUNT];
    int index;
#if defined(_WIN32)
    HANDLE threads[THREAD_COUNT] = { NULL };
    for (index = 0; index < THREAD_COUNT; index++) {
        states[index].profile = profile;
        states[index].passed = 0;
        threads[index] = CreateThread(NULL, 0, worker_entry, &states[index], 0, NULL);
        if (threads[index] == NULL) {
            goto fail;
        }
    }
    if (WaitForMultipleObjects(THREAD_COUNT, threads, TRUE, INFINITE) != WAIT_OBJECT_0) {
        goto fail;
    }
    for (index = 0; index < THREAD_COUNT; index++) {
        DWORD exit_code = 1;
        if (!GetExitCodeThread(threads[index], &exit_code) || exit_code != 0 || !states[index].passed) {
            goto fail;
        }
        CloseHandle(threads[index]);
    }
    return 1;

fail:
    for (index = 0; index < THREAD_COUNT; index++) {
        if (threads[index] != NULL) {
            WaitForSingleObject(threads[index], INFINITE);
            CloseHandle(threads[index]);
        }
    }
    return 0;
#else
    pthread_t threads[THREAD_COUNT];
    int created = 0;
    for (index = 0; index < THREAD_COUNT; index++) {
        states[index].profile = profile;
        states[index].passed = 0;
        if (pthread_create(&threads[index], NULL, worker_entry, &states[index]) != 0) {
            goto fail;
        }
        created++;
    }
    for (index = 0; index < created; index++) {
        void* result = NULL;
        if (pthread_join(threads[index], &result) != 0 || (intptr_t) result != 0 || !states[index].passed) {
            return 0;
        }
    }
    return 1;

fail:
    for (index = 0; index < created; index++) {
        pthread_join(threads[index], NULL);
    }
    return 0;
#endif
}

int main(int argc, char** argv)
{
    char runtime_path[4096];
    cmsContext context = NULL;
    ProfileBytes lut_profile = { NULL, 0 };
    int version;
    int matrix_passed;
    int lut_passed;
    MalformedResults malformed = { 0, 0, 0, 0, 0 };
    int malformed_passed;
    int concurrency_passed;
    int result = 1;

    if (argc != 2) {
        fprintf(stderr, "usage: %s <expected-runtime-directory>\n", argv[0]);
        return 2;
    }

    if (!get_runtime_path(runtime_path, sizeof(runtime_path)) || !runtime_is_local(runtime_path, argv[1])) {
        fprintf(stderr, "runtime locality validation failed\n");
        return 1;
    }

    version = cmsGetEncodedCMMversion();
    printf("runtime.path=%s\n", runtime_path);
    printf("runtime.version=%d.%d\n", version / 1000, (version % 1000) / 10);
    printf("runtime.versionEncoded=%d\n", version);
    if (version != EXPECTED_VERSION) {
        fprintf(stderr, "unexpected Little CMS runtime version\n");
        return 1;
    }

    context = cmsCreateContext(NULL, NULL);
    if (context == NULL) {
        fprintf(stderr, "cmsCreateContext failed\n");
        return 1;
    }
    cmsSetLogErrorHandlerTHR(context, quiet_error_handler);

    matrix_passed = run_matrix_transform(context);
    if (!create_lut_profile(context, &lut_profile)) {
        lut_passed = 0;
        malformed_passed = 0;
        concurrency_passed = 0;
    } else {
        lut_passed = transform_lut_bytes(context, &lut_profile, 1);
        malformed = check_malformed_profiles(context, &lut_profile);
        malformed_passed = malformed.empty && malformed.bad_signature && malformed.truncated &&
            malformed.impossible_size && malformed.oversized_admission;
        concurrency_passed = run_concurrency(&lut_profile);
    }

    printf("matrixTransform=%s\n", matrix_passed ? "PASS" : "FAIL");
    printf("lutProfile.class=CLUT\n");
    printf("lutTransform=%s\n", lut_passed ? "PASS" : "FAIL");
    printf("malformed.empty=%s\n", malformed.empty ? "PASS" : "FAIL");
    printf("malformed.badSignature=%s\n", malformed.bad_signature ? "PASS" : "FAIL");
    printf("malformed.truncated=%s\n", malformed.truncated ? "PASS" : "FAIL");
    printf("malformed.impossibleSize=%s\n", malformed.impossible_size ? "PASS" : "FAIL");
    printf("admission.over16MiB=%s\n", malformed.oversized_admission ? "PASS" : "FAIL");
    printf("concurrency.independentTransforms=%s\n", concurrency_passed ? "PASS" : "FAIL");
    printf("intent=relative-colorimetric\n");
    printf("blackPointCompensation=0\n");

    if (!matrix_passed || !lut_passed || !malformed_passed || !concurrency_passed) {
        result = 1;
    } else {
        result = 0;
    }
    printf("result=%s\n", result == 0 ? "PASS" : "FAIL");

    free(lut_profile.data);
    cmsDeleteContext(context);
    return result;
}
