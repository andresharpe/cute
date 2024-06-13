using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Lib.Serializers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cute.Tests;

public class JsonExtensionsTest
{
    [Fact]
    public void DeserializeAndCondenseContentfulEntry_Should_ProduceCutEntryStructure()
    {
        var entry = JObject.Parse("""
            {
              "sys": {
                "Id": "73rWjTVUTvS5pgOfEsxnKS",
                "Type": "Entry",
                "Version": 51,
                "CreatedAt": "2024-05-23T14:27:53.74Z",
                "CreatedBy": {
                  "sys": {
                    "Id": "5G65pISkj6AswuRbl7vWEj",
                    "LinkType": "User",
                    "Type": "Link",
                  }
                },
                "UpdatedAt": "2024-06-01T20:13:24.669Z",
                "UpdatedBy": {
                  "sys": {
                    "Id": "5G65pISkj6AswuRbl7vWEj",
                    "LinkType": "User",
                    "Type": "Link",
                  }
                },
                "ContentType": {
                  "sys": {
                    "Id": "dataGeo",
                    "LinkType": "ContentType",
                    "Type": "Link",
                  }
                },
                "Space": {
                  "sys": {
                    "Id": "s6kotwamg811",
                    "LinkType": "Space",
                    "Type": "Link",
                  }
                },
                "PublishedCounter": 21,
                "PublishedVersion": 50,
                "PublishedBy": {
                  "sys": {
                    "Id": "5G65pISkj6AswuRbl7vWEj",
                    "LinkType": "User",
                    "Type": "Link",
                  }
                },
                "PublishedAt": "2024-06-01T20:13:24.669Z",
                "FirstPublishedAt": "2024-05-23T15:17:42.944Z",
                "Environment": {
                  "sys": {
                    "Id": "master",
                    "LinkType": "Environment",
                    "Type": "Link",
                  }
                }
              },
              "$metadata": {
                "Tags": []
              },
              "Fields": {
                "key": {
                  "en": "2"
                },
                "title": {
                  "en": "Aalst",
                  "fr": "Alost"
                },
                "heroImage": {
                  "en": {
                    "sys": {
                      "type": "Link",
                      "linkType": "Asset",
                      "id": "5V1ZvUpjYw3YfSoOU1qxun"
                    }
                  }
                },
                "aboutTitle": {
                  "en": "about Aalst",
                  "fr": "about Aalst"
                },
                "inTitle": {
                  "en": "in Aalst",
                  "fr": "à Alost"
                },
                "nearTitle": {
                  "en": "near Aalst",
                  "fr": "near Aalst"
                },
                "ofTitle": {
                  "en": "Aalst's",
                  "fr": "Aalst's"
                },
                "latLong": {
                  "en": {
                    "lon": 4.074311,
                    "lat": 50.945359
                  },
                  "fr": {
                    "lon": 4.074311,
                    "lat": 50.945359
                  }
                },
                "order": {
                  "en": 99,
                  "fr": 100
                },
                "shortDescription": {
                  "en": {
                    "data": {
                      "target": null
                    },
                    "content": [
                      {
                        "data": {
                          "target": null
                        },
                        "content": [
                          {
                            "data": {
                              "target": null
                            },
                            "marks": [],
                            "value": "Establish your business in Aalst, between the historic cities of Brussels to the east and Ghent to the west. Choose our modern office space here and find room to flourish in this strategic location.",
                            "nodeType": "text"
                          }
                        ],
                        "nodeType": "paragraph"
                      }
                    ],
                    "nodeType": "document"
                  },
                  "fr": {
                    "data": {
                      "target": null
                    },
                    "content": [
                      {
                        "data": {
                          "target": null
                        },
                        "content": [
                          {
                            "data": {
                              "target": null
                            },
                            "marks": [],
                            "value": "Implantez votre entreprise à Alost, entre les villes historiques de Bruxelles à l'est et de Gand à l'ouest. Choisissez l'un de nos bureaux modernes pour développer votre entreprise dans cette région stratégique.",
                            "nodeType": "text"
                          }
                        ],
                        "nodeType": "paragraph"
                      }
                    ],
                    "nodeType": "document"
                  }
                },
                "longDescription": {
                  "en": {
                    "data": {
                      "target": null
                    },
                    "content": [
                      {
                        "data": {
                          "target": null
                        },
                        "content": [
                          {
                            "data": {
                              "target": null
                            },
                            "marks": [],
                            "value": "Pursue your business goals in the city of Aalst. Choose modern office space here and take advantage of our flexible terms: as much or as little space as you need, for as long as you need it. Customise any of our spaces to suit your business, and get started straight away, with high-speed WiFi and ergonomic furniture all ready to use when you arrive. ",
                            "nodeType": "text"
                          }
                        ],
                        "nodeType": "paragraph"
                      }
                    ],
                    "nodeType": "document"
                  },
                  "fr": {
                    "data": {
                      "target": null
                    },
                    "content": [
                      {
                        "data": {
                          "target": null
                        },
                        "content": [
                          {
                            "data": {
                              "target": null
                            },
                            "marks": [],
                            "value": "Atteignez vos objectifs professionnels dans la ville d'Alost. Choisissez un bureau moderne et profitez de nos conditions flexibles : tout l'espace dont vous avez besoin, aussi longtemps que vous en avez besoin. Personnalisez l'un de nos bureaux en fonction des besoins de votre entreprise, et lancez-vous immédiatement grâce au Wi-Fi haut débit et à un mobilier ergonomique, le tout prêt à l'emploi dès votre arrivée.",
                            "nodeType": "text"
                          }
                        ],
                        "nodeType": "paragraph"
                      }
                    ],
                    "nodeType": "document"
                  }
                },
                "slug": {
                  "en": "aalst",
                  "fr": "alost"
                },
                "dataGeoParent": {
                  "en": {
                    "sys": {
                      "type": "Link",
                      "linkType": "Entry",
                      "id": "6i2gQGYVFq6pAejUzMfkSv"
                    }
                  }
                },
                "hidePhoneNumber": {
                  "en": false,
                  "fr": false
                },
                "anchorLinkToMap": {
                  "en": "#273fe61d-e3fa-4d45-b2ed-7e8a367e94c1",
                  "fr": "#273fe61d-e3fa-4d45-b2ed-7e8a367e94c1"
                },
                "includeMetaTagRobot": {
                  "en": false,
                  "fr": false
                },
                "metaTagRobotContent": {
                  "en": "noindex, nofollow",
                  "fr": "noindex, nofollow"
                },
                "includeHrefLang": {
                  "en": false,
                  "fr": false
                },
                "brand": {
                  "en": [
                    {
                      "sys": {
                        "type": "Link",
                        "linkType": "Entry",
                        "id": "3i5v8BOH5jiG5f2aNKdEEV"
                      }
                    },
                    {
                      "sys": {
                        "type": "Link",
                        "linkType": "Entry",
                        "id": "exP8sIRYdwu7AhTSxyMog"
                      }
                    }
                  ]
                }
              }
            }
            """).ToObject<Entry<JObject>>() ?? new(); ;

        var locales = JArray.Parse("""
            [
                {
                  "sys": {
                    "Id": "5LN4EBrhdHo4yNnxGAwzX3",
                    "Type": "Locale",
                    "Version": 2,
                  },
                  "Name": "English",
                  "Code": "en",
                  "FallbackCode": null,
                  "Default": true,
                  "Optional": false,
                  "ContentManagementApi": true,
                  "ContentDeliveryApi": true
                },
                {
                  "sys": {
                    "Id": "5pmyXDbI5iG7ZcaTijTAD2",
                    "Type": "Locale",
                    "Version": 1,
                  },
                  "Name": "French",
                  "Code": "fr",
                  "FallbackCode": "en",
                  "Default": false,
                  "Optional": false,
                  "ContentManagementApi": true,
                  "ContentDeliveryApi": true
                }
            ]
            """).ToObject<List<Locale>>() ?? new();

        var contentType = JObject.Parse("""
            {
              "Name": "DataGeo",
              "Description": "A hierarchical geographic structure of countries, optional states, cities and suburbs  ",
              "DisplayField": "title",
              "Fields": [
                {
                  "Id": "key",
                  "Name": "Key",
                  "Type": "Symbol",
                  "Required": true,
                  "Localized": false
                },
                {
                  "Id": "title",
                  "Name": "Title",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "heroImage",
                  "Name": "HeroImage",
                  "Type": "Link",
                  "LinkType": "Asset",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "aboutTitle",
                  "Name": "AboutTitle",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "inTitle",
                  "Name": "InTitle",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "nearTitle",
                  "Name": "NearTitle",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "ofTitle",
                  "Name": "OfTitle",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "latLong",
                  "Name": "LatLong",
                  "Type": "Location",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "order",
                  "Name": "Order",
                  "Type": "Integer",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "icon",
                  "Name": "Icon",
                  "Type": "Link",
                  "LinkType": "Asset",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "shortDescription",
                  "Name": "ShortDescription",
                  "Type": "RichText",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "longDescription",
                  "Name": "LongDescription",
                  "Type": "RichText",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "slug",
                  "Name": "Slug",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": true
                },
                {
                  "Id": "dataGeoParent",
                  "Name": "DataGeoParent",
                  "Type": "Link",
                  "LinkType": "Entry",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "hidePhoneNumber",
                  "Name": "HidePhoneNumber",
                  "Type": "Boolean",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "anchorLinkToMap",
                  "Name": "AnchorLinkToMap",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "includeMetaTagRobot",
                  "Name": "IncludeMetaTagRobot",
                  "Type": "Boolean",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "metaTagRobotContent",
                  "Name": "MetaTagRobotContent",
                  "Type": "Symbol",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "includeHrefLang",
                  "Name": "IncludeHrefLang",
                  "Type": "Boolean",
                  "Required": false,
                  "Localized": false
                },
                {
                  "Id": "brand",
                  "Name": "Brand",
                  "Type": "Array",
                  "Items": {
                    "Type": "Link",
                    "LinkType": "Entry",
                    "Validations": [
                    {
                        "linkContentType": [
                            "dataBrand"
                        ],
                        "message": null
                    }]
                  },
                  "Required": false,
                  "Localized": false
                }
              ]
            }
            """).ToObject<ContentType>() ?? new();

        var cutSerializer = new EntrySerializer(contentType, locales);

        var flatEntry = cutSerializer.SerializeEntry(entry);

        var flatEntryJson = JsonConvert.SerializeObject(flatEntry, Formatting.Indented);

        var newEntry = cutSerializer.DeserializeEntry(flatEntry);

        var newEntryJson = JsonConvert.SerializeObject(newEntry, Formatting.Indented);

        flatEntryJson.Should().NotBeNull();

        newEntryJson.Should().NotBeNull();
    }
}