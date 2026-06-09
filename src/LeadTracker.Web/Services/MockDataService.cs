using LeadTracker.Web.Models;

namespace LeadTracker.Web.Services;

public class MockDataService
{
    private static List<Message> M(params (string From, string Text, string Time)[] msgs) =>
        msgs.Select(m => new Message(m.From, m.Text, m.Time)).ToList();

    public List<Lead> GetLeads() =>
    [
        new(0, "Rodrigo Píriz", "+598 99 412 087", Temperatura.Caliente,
            "Venta Pocitos — derivado a asesor", 0, "10:24", true, M(
            ("lead", "Hola, vi el apartamento en venta en Pocitos de 2 dormitorios, quería info", "10:18"),
            ("bot",  "¡Hola! 👋 Soy Vera, asistente de Bahía Propiedades. El apto de Pocitos es a estrenar, 2 dormitorios, 68 m² con cochera. ¿Querés que te pase más detalles?", "10:18"),
            ("lead", "sí, y el precio?", "10:21"),
            ("bot",  "Está publicado en USD 168.000. El margen de negociación lo maneja directamente uno de nuestros asesores. ¿Querés que te contacten?", "10:21"),
            ("lead", "dale, me interesa hablar del precio", "10:23"),
            ("bot",  "¡Genial! ¿Me dejás un teléfono o mail para que un asesor se comunique con vos?", "10:23"),
            ("lead", "099 412 087, rodrigo.piriz@gmail.com", "10:24"),
            ("bot",  "¡Perfecto Rodrigo! Ya le paso tus datos a un asesor para que te contacte 🏠", "10:24"))),

        new(1, "Valentina Methol", "+598 98 305 771", Temperatura.Caliente,
            "Alquiler Punta Carretas — derivada a asesor", 0, "09:47", true, M(
            ("lead", "Buenas! el apto en alquiler de Punta Carretas sigue disponible?", "09:40"),
            ("bot",  "¡Hola! Soy Vera 🏠 Sí, sigue disponible: 1 dormitorio, muy luminoso, alquiler $32.000 + gastos. ¿Querés más info?", "09:40"),
            ("lead", "sí me interesa, quiero verlo", "09:45"),
            ("bot",  "¡Genial! Las visitas las coordina directamente un asesor. ¿Me dejás un teléfono o mail para que te contacten?", "09:45"),
            ("lead", "098 305 771", "09:47"),
            ("bot",  "¡Gracias Valentina! Le paso tus datos a un asesor para que se comunique ✨", "09:47"))),

        new(2, "Camila Suárez", "+598 91 220 654", Temperatura.Tibio,
            "Venta Carrasco — comparando", 0, "08:55", true, M(
            ("lead", "hola, tienen casas en venta en Carrasco?", "08:50"),
            ("bot",  "¡Hola! Sí, tenemos varias opciones en Carrasco 🏡 ¿Buscás de cuántos dormitorios y en qué rango de precio?", "08:50"),
            ("lead", "3 dormitorios, más o menos qué presupuesto?", "08:53"),
            ("bot",  "En 3 dormitorios con jardín los valores arrancan en USD 320.000. ¿Querés que un asesor te contacte?", "08:53"),
            ("lead", "lo voy a pensar, gracias", "08:55"),
            ("bot",  "¡Dale! Quedo por acá. Si querés te aviso cuando entren nuevas propiedades en la zona 📬", "08:55"))),

        new(3, "Joaquín Lema", "+598 99 887 102", Temperatura.Frio,
            "Consulta — visitas el fin de semana", 0, "08:12", false, M(
            ("lead", "hacen visitas los sábados?", "08:10"),
            ("bot",  "¡Hola! Sí, los asesores coordinan visitas los sábados de 9:00 a 13:00 📅", "08:10"),
            ("lead", "no, gracias era solo eso", "08:12"),
            ("bot",  "¡Perfecto! Quedo a las órdenes 🙌", "08:12"))),

        new(4, "Florencia Tabárez", "+598 92 044 318", Temperatura.Caliente,
            "Alquiler Malvín — derivada a asesor", 1, "17:30", false, M(
            ("lead", "busco alquilar un apto en Malvín, 1 o 2 dormitorios", "17:20"),
            ("bot",  "¡Hola! Tengo un 2 dormitorios en Malvín a una cuadra de la rambla, $41.000 + gastos 🏖 ¿Te interesa?", "17:21"),
            ("lead", "siii, cómo sigo?", "17:25"),
            ("bot",  "Te derivo con un asesor que coordina todo. ¿Me dejás tu teléfono o mail?", "17:25"),
            ("lead", "092 044 318, flor.tabarez@gmail.com", "17:30"),
            ("bot",  "¡Listo Florencia! Le paso tus datos a un asesor para que te contacte 🏠", "17:30"))),

        new(5, "Diego Methol", "+598 94 671 200", Temperatura.Tibio,
            "Venta Punta del Este — pidió info", 1, "12:08", false, M(
            ("lead", "tienen algo en venta en Punta del Este cerca de la playa?", "12:00"),
            ("bot",  "¡Sí! Tenemos apartamentos frente al mar en la Mansa y la Brava. ¿Buscás para vivir, veranear o como inversión?", "12:01"),
            ("lead", "más como inversión, 1 dormitorio", "12:05"),
            ("bot",  "Perfecto. ¿Querés que un asesor te contacte con números de rentabilidad?", "12:06"),
            ("lead", "después veo, ando con poco tiempo ahora", "12:08"),
            ("bot",  "¡Sin problema! Cuando quieras me escribís y seguimos 👍", "12:08"))),

        new(6, "Agustina Mancebo", "+598 96 510 449", Temperatura.Frio,
            "Preguntó ubicación de un inmueble", 2, "19:42", false, M(
            ("lead", "dónde queda el apto de la publicación?", "19:40"),
            ("bot",  "¡Hola! El apartamento está en Bulevar España y Br. Artigas 📍 ¿Querés que un asesor te pase más info?", "19:40"),
            ("lead", "ah ok gracias", "19:42"))),

        new(7, "Sebastián Píriz", "+598 99 023 765", Temperatura.Caliente,
            "Inversión en pozo — derivado a asesor", 2, "08:30", false, M(
            ("lead", "estoy buscando comprar para invertir, tienen proyectos en pozo?", "08:22"),
            ("bot",  "¡Hola! Sí, tenemos unidades en pozo con financiación y entrega 2026 🏗️ ¿Querés que un asesor te muestre las opciones?", "08:22"),
            ("lead", "sí, me interesa", "08:28"),
            ("bot",  "¡Perfecto! ¿Me dejás un teléfono o mail?", "08:28"),
            ("lead", "099 023 765, sebapiriz@gmail.com", "08:30"),
            ("bot",  "¡Gracias Sebastián! Le paso tus datos a un asesor de inversiones 🏆", "08:30"))),

        new(8, "Lucía Fernández", "+598 91 778 320", Temperatura.Tibio,
            "Alquiler Cordón — duda de garantía", 3, "14:15", false, M(
            ("lead", "hola cuánto el alquiler del apto en Cordón?", "14:10"),
            ("bot",  "¡Hola Lucía! El de Cordón está en $28.000 + $3.200 de gastos. ¿Querés más info?", "14:10"),
            ("lead", "y qué garantía piden? no tengo propietario", "14:13"),
            ("bot",  "Se puede con ANDA, Porto Seguro o seguro de alquiler 👍 ¿Querés que un asesor te contacte?", "14:14"),
            ("lead", "lo veo y te aviso", "14:15"),
            ("bot",  "¡Dale! Quedo por acá 📬", "14:15"))),

        new(9, "Martín Rodríguez", "+598 98 119 562", Temperatura.Frio,
            "Sin respuesta tras saludo", 4, "21:03", false, M(
            ("lead", "info", "21:00"),
            ("bot",  "¡Hola! Soy Vera de Bahía Propiedades 🏠 ¿Estás buscando alquilar o comprar?", "21:00"),
            ("bot",  "Quedo por acá cuando quieras 👋", "21:03"))),

        new(10, "Paula Methol", "+598 95 660 871", Temperatura.Tibio,
            "Venta — casa para la familia", 5, "11:20", false, M(
            ("lead", "busco una casa para mudarme con mis hijos, zona tranquila", "11:12"),
            ("bot",  "¡Hola Paula! ¿Tenés alguna zona en mente y cuántos dormitorios necesitás?", "11:13"),
            ("lead", "Carrasco o Punta Gorda, 3 dormitorios. tendría que verlo con mi marido", "11:18"),
            ("bot",  "¡Dale! Te preparo una selección y cuando lo definan, un asesor coordina las visitas 🏡", "11:20"))),

        new(11, "Nicolás Bentancor", "+598 99 340 118", Temperatura.Caliente,
            "Venta La Blanqueada — derivado a asesor", 6, "16:40", false, M(
            ("lead", "buenas, vi la casa en venta en La Blanqueada, quiero más info", "16:30"),
            ("bot",  "¡Hola Nicolás! La casa de La Blanqueada es de 3 dormitorios con fondo y garaje, USD 215.000. ¿Querés que un asesor te contacte?", "16:31"),
            ("lead", "sí, quiero coordinar para verla", "16:35"),
            ("bot",  "¡Genial! ¿Me dejás un teléfono o mail?", "16:36"),
            ("lead", "099 340 118", "16:40"),
            ("bot",  "¡Listo! Le paso tus datos a un asesor 🏠", "16:40"))),

        new(12, "Carolina Silva", "+598 92 887 005", Temperatura.Frio,
            "Cotización de alquiler, no respondió", 8, "10:05", false, M(
            ("lead", "precio del alquiler en Buceo?", "10:02"),
            ("bot",  "¡Hola! El 2 dormitorios de Buceo está en $36.000 + gastos 🏠 ¿Querés que un asesor te contacte?", "10:02"),
            ("lead", "ok gracias", "10:05"))),

        new(13, "Gonzalo Methol", "+598 99 551 423", Temperatura.Tibio,
            "Venta Buceo — consulta financiación", 11, "13:50", false, M(
            ("lead", "el apto en venta de Buceo se puede comprar con crédito hipotecario?", "13:45"),
            ("bot",  "¡Hola Gonzalo! Sí, varias propiedades califican para préstamo del BHU y bancos privados 🏦", "13:46"),
            ("lead", "tengo como el 40% para la seña", "13:48"),
            ("bot",  "Perfecto, con ese anticipo entrás muy bien. ¿Querés que un asesor te contacte?", "13:49"),
            ("lead", "lo veo y te aviso", "13:50"))),

        new(14, "Romina Pereira", "+598 94 209 663", Temperatura.Caliente,
            "Alquiler Parque Rodó — derivada a asesor", 13, "09:15", false, M(
            ("lead", "quiero alquilar un apto en Parque Rodó, para entrar pronto", "09:08"),
            ("bot",  "¡Hola Romina! Tengo un 1 dormitorio en Parque Rodó, disponible ya, $30.000 + gastos 🏠", "09:09"),
            ("lead", "sí dale, te dejo mi mail: romina.pereira@gmail.com", "09:14"),
            ("bot",  "¡Perfecto! Le paso tus datos a un asesor para que se comunique a la brevedad 🏠", "09:15"))),

        new(15, "Federico Olivera", "+598 98 770 214", Temperatura.Frio,
            "Consulta sobre comisión", 18, "18:22", false, M(
            ("lead", "cuánto cobran de comisión por el alquiler?", "18:20"),
            ("bot",  "La comisión es de un mes de alquiler + IVA, una sola vez al firmar 📝 ¿Querés ver propiedades?", "18:20"),
            ("lead", "no gracias", "18:22"))),

        new(16, "Mariana Cabrera", "+598 91 503 887", Temperatura.Tibio,
            "Venta Punta Gorda — interesada", 22, "12:40", false, M(
            ("lead", "vi en instagram la casa de Punta Gorda, me encantó", "12:35"),
            ("bot",  "¡Gracias Mariana! 😊 Es una casa de 4 dormitorios con piscina, a metros del río. ¿Querés que un asesor te contacte?", "12:36"),
            ("lead", "sí pero después de fin de mes", "12:40"),
            ("bot",  "¡Perfecto! Te escribo a fin de mes para retomarlo 🗓️", "12:40"))),

        new(17, "Andrés Methol", "+598 99 118 740", Temperatura.Caliente,
            "Alquiler Centro — derivado a asesor", 29, "15:05", false, M(
            ("lead", "necesito alquilar algo en el Centro, lo antes posible", "15:00"),
            ("bot",  "¡Hola Andrés! Tengo un monoambiente y un 1 dormitorio en el Centro disponibles ya. ¿Querés que un asesor te contacte?", "15:01"),
            ("lead", "sí, lo antes posible", "15:03"),
            ("bot",  "¿Me dejás un teléfono o mail?", "15:04"),
            ("lead", "099 118 740", "15:05"),
            ("bot",  "¡Gracias Andrés! Le paso tus datos a un asesor 🏠", "15:05"))),
    ];
}
