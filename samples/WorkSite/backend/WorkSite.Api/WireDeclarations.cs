using CodeBrix.WebPlatform.Core.Mapping;

//What WorkSite's backend declares about itself for the generated frontend code. The value is the
//one RequestedWithMiddleware demands in X-Requested-With (it reads it back from here).
[assembly: WireRequestMarker("WorkSite")]
