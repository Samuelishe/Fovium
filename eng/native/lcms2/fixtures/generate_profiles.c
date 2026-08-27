#include <lcms2.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#if defined(_WIN32)
#define PATH_SEPARATOR "\\"
#else
#define PATH_SEPARATOR "/"
#endif

static int write_text_tag(cmsHPROFILE profile, cmsTagSignature tag, const char* text)
{
    cmsMLU* value = cmsMLUalloc(NULL, 1);
    int result;
    if (value == NULL) {
        return 0;
    }
    result = cmsMLUsetASCII(value, "en", "US", text) && cmsWriteTag(profile, tag, value);
    cmsMLUfree(value);
    return result;
}

static cmsHPROFILE create_linear_display(void)
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
        curves[index] = cmsBuildGamma(NULL, 1.0);
        if (curves[index] == NULL) {
            goto cleanup;
        }
    }
    profile = cmsCreateRGBProfile(&white_point, &primaries, curves);
    if (profile != NULL) {
        cmsSetProfileVersion(profile, 4.3);
        if (!write_text_tag(profile, cmsSigProfileDescriptionTag, "Fovium synthetic linear RGB display") ||
            !write_text_tag(profile, cmsSigCopyrightTag, "Project-authored Fovium test data")) {
            cmsCloseProfile(profile);
            profile = NULL;
        }
    }

cleanup:
    for (index = 0; index < 3; index++) {
        if (curves[index] != NULL) {
            cmsFreeToneCurve(curves[index]);
        }
    }
    return profile;
}

static int lut_sampler(const cmsUInt16Number input[], cmsUInt16Number output[], void* cargo)
{
    (void) cargo;
    output[0] = input[2];
    output[1] = (cmsUInt16Number) (65535u - input[1]);
    output[2] = input[0];
    return 1;
}

static cmsHPROFILE create_lut_display(void)
{
    cmsHPROFILE profile = cmsCreateProfilePlaceholder(NULL);
    cmsPipeline* pipeline = cmsPipelineAlloc(NULL, 3, 3);
    cmsStage* input_curves = cmsStageAllocToneCurves(NULL, 3, NULL);
    cmsStage* clut = cmsStageAllocCLut16bit(NULL, 2, 3, 3, NULL);
    cmsStage* output_curves = cmsStageAllocToneCurves(NULL, 3, NULL);
    cmsCIEXYZ d50 = *cmsD50_XYZ();
    int passed = 0;

    if (profile == NULL || pipeline == NULL || input_curves == NULL || clut == NULL || output_curves == NULL) {
        goto cleanup;
    }
    cmsSetProfileVersion(profile, 4.3);
    cmsSetDeviceClass(profile, cmsSigDisplayClass);
    cmsSetColorSpace(profile, cmsSigRgbData);
    cmsSetPCS(profile, cmsSigLabData);
    cmsSetHeaderRenderingIntent(profile, INTENT_RELATIVE_COLORIMETRIC);
    if (!write_text_tag(profile, cmsSigProfileDescriptionTag, "Fovium synthetic LUT RGB display") ||
        !write_text_tag(profile, cmsSigCopyrightTag, "Project-authored Fovium test data") ||
        !cmsWriteTag(profile, cmsSigMediaWhitePointTag, &d50) ||
        !cmsStageSampleCLut16bit(clut, lut_sampler, NULL, 0) ||
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
    if (!cmsWriteTag(profile, cmsSigBToA0Tag, pipeline)) {
        goto cleanup;
    }
    passed = 1;

cleanup:
    if (output_curves != NULL) cmsStageFree(output_curves);
    if (clut != NULL) cmsStageFree(clut);
    if (input_curves != NULL) cmsStageFree(input_curves);
    if (pipeline != NULL) cmsPipelineFree(pipeline);
    if (!passed && profile != NULL) {
        cmsCloseProfile(profile);
        profile = NULL;
    }
    return profile;
}

static int save_profile(cmsHPROFILE profile, const char* directory, const char* filename)
{
    char path[4096];
    if (snprintf(path, sizeof(path), "%s%s%s", directory, PATH_SEPARATOR, filename) <= 0) {
        return 0;
    }
    return cmsSaveProfileToFile(profile, path);
}

static int print_reference(const char* label, cmsHPROFILE destination)
{
    static const unsigned char input[][4] = {
        { 0, 0, 0, 255 },
        { 129, 63, 17, 255 },
        { 240, 192, 128, 128 },
        { 255, 255, 255, 1 }
    };
    unsigned char output[sizeof(input) / sizeof(input[0])][4] = { 0 };
    cmsHPROFILE source = cmsCreate_sRGBProfile();
    cmsHTRANSFORM transform = NULL;
    size_t index;
    if (source == NULL) {
        return 0;
    }
    transform = cmsCreateTransform(
        source,
        TYPE_BGRA_8,
        destination,
        TYPE_BGRA_8,
        INTENT_RELATIVE_COLORIMETRIC,
        cmsFLAGS_COPY_ALPHA);
    if (transform == NULL) {
        cmsCloseProfile(source);
        return 0;
    }
    cmsDoTransform(transform, input, output, (cmsUInt32Number) (sizeof(input) / sizeof(input[0])));
    for (index = 0; index < sizeof(input) / sizeof(input[0]); index++) {
        printf("%s.patch%zu=%u,%u,%u,%u\n", label, index,
            output[index][0], output[index][1], output[index][2], output[index][3]);
    }
    cmsDeleteTransform(transform);
    cmsCloseProfile(source);
    return 1;
}

int main(int argc, char** argv)
{
    cmsHPROFILE matrix;
    cmsHPROFILE lut;
    int passed;
    if (argc != 2) {
        fprintf(stderr, "usage: %s <output-directory>\n", argv[0]);
        return 2;
    }
    if (cmsGetEncodedCMMversion() != 2190) {
        fprintf(stderr, "Little CMS 2.19 is required\n");
        return 1;
    }
    matrix = create_linear_display();
    lut = create_lut_display();
    passed = matrix != NULL && lut != NULL &&
        save_profile(matrix, argv[1], "fovium-linear-rgb-display.icc") &&
        save_profile(lut, argv[1], "fovium-lut-rgb-display.icc") &&
        print_reference("matrix", matrix) &&
        print_reference("lut", lut);
    if (lut != NULL) cmsCloseProfile(lut);
    if (matrix != NULL) cmsCloseProfile(matrix);
    printf("runtime.versionEncoded=%u\n", cmsGetEncodedCMMversion());
    printf("result=%s\n", passed ? "PASS" : "FAIL");
    return passed ? 0 : 1;
}
